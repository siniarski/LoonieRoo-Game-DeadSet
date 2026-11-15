using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Core;
using Deadset.Effects;
using Deadset.Enemies;
using Godot;

namespace Deadset.Waves;

/// <summary>
/// Runs the wave loop: countdown, wave (spawns packs at the arena's spawn points),
/// cleared, trader break, next wave. Announces every step on GameEvents.
/// Read-only properties let the HUD show countdowns and enemies left.
/// </summary>
public partial class WaveDirector : Node
{
	public enum Phase { Countdown, Wave, Break, Stopped }

	/// <summary>A vent started rattling; Count enemies come out after VentWarning seconds.</summary>
	[Signal] public delegate void VentOpenedEventHandler(Node3D vent, int count);

	[Export] public WaveSet Waves { get; set; }
	/// <summary>Spawn points closer than this to the player are skipped (if any others exist).</summary>
	[Export] public float MinSpawnDistance { get; set; } = 16f;
	/// <summary>Enemies in a pack scatter within this radius, in metres.</summary>
	[Export] public float ScatterRadius { get; set; } = 1.8f;
	/// <summary>Enemies below this height fell out of the world and are removed.</summary>
	[Export] public float KillHeight { get; set; } = -20f;
	/// <summary>
	/// Most enemies created in one physics tick. A big pack trickles in over a few ticks
	/// instead of landing in a single frame, which would hitch.
	/// </summary>
	[Export] public int MaxSpawnsPerTick { get; set; } = 3;
	/// <summary>How many enemies may be mid-attack at once, however many surround the player.</summary>
	[Export] public int MaxSimultaneousAttackers { get; set; } = 6;

	[ExportGroup("Vents")]
	/// <summary>
	/// Packs can climb out of floor vents near the player instead of arriving at a gate, so no spot
	/// is only ever attacked from one side. More likely when the player camps in one place.
	/// </summary>
	[Export(PropertyHint.Range, "0,1")] public float VentChance { get; set; } = 0.12f;
	/// <summary>Vent chance once the player has stayed within CampRadius for CampSeconds.</summary>
	[Export(PropertyHint.Range, "0,1")] public float CampingVentChance { get; set; } = 0.5f;
	[Export] public float CampRadius { get; set; } = 7f;
	[Export] public float CampSeconds { get; set; } = 6f;
	/// <summary>Only vents between these distances from the player are used.</summary>
	[Export] public float VentMinDistance { get; set; } = 5f;
	[Export] public float VentMaxDistance { get; set; } = 16f;
	/// <summary>Most enemies out of one vent per pack; the rest of the pack uses a gate.</summary>
	[Export] public int VentPackMax { get; set; } = 4;
	/// <summary>Seconds the vent rattles before anything comes out.</summary>
	[Export] public float VentWarning { get; set; } = 1.3f;
	/// <summary>Seconds before the same vent can be used again.</summary>
	[Export] public float VentCooldown { get; set; } = 8f;
	[Export] public Color VentSparkColor { get; set; } = new(0.4f, 1f, 0.35f);
	[ExportGroup("")]

	public Phase CurrentPhase { get; private set; } = Phase.Stopped;
	/// <summary>True while the player counts as camping (vents are more likely).</summary>
	public bool PlayerCamping => _campTimer >= CampSeconds;
	/// <summary>The wave in progress, or the next one during a countdown or break. Starts at 1.</summary>
	public int WaveNumber { get; private set; }
	/// <summary>Seconds left in the current countdown or break.</summary>
	public float TimeRemaining { get; private set; }
	/// <summary>Enemies still to spawn plus those alive, in the current wave.</summary>
	public int EnemiesRemaining => _queuedCount + _alive.Count;

	private struct Pack
	{
		public PackedScene Scene;
		public int Count;
		public float Order;
	}

	private struct PendingSpawn
	{
		public PackedScene Scene;
		public Vector3 Position;
	}

	private readonly List<Pack> _queue = new();
	/// <summary>Enemies of packs already released, waiting their turn to be created.</summary>
	private readonly Queue<PendingSpawn> _pending = new();
	private readonly HashSet<Node3D> _alive = new();
	private readonly List<Node3D> _fallen = new();
	private readonly List<Node3D> _farPoints = new();

	private class VentBurst
	{
		public Node3D Vent;
		public PackedScene Scene;
		public int Count;
		public float Timer;
		public float NextRattle;
	}

	private readonly List<VentBurst> _ventBursts = new();
	private readonly Dictionary<Node3D, double> _ventReadyAt = new();
	private readonly List<Node3D> _ventCandidates = new();
	private int _ventWaiting;
	private Vector3 _campAnchor;
	private float _campTimer;
	private int _queuedCount;
	private int _maxAlive;
	private float _packInterval;
	private float _packTimer;
	private float _cullTimer;
	private Node3D _player;

	public override void _Ready()
	{
		Deadset.Enemies.AttackSlots.MaxAttackers = MaxSimultaneousAttackers;
		AddToGroup("wave_director");
		GameEvents.Instance.EnemyKilled += OnEnemyKilled;

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		HealthComponent playerHealth = HealthComponent.FindOn(_player);
		if (playerHealth != null)
			playerHealth.Died += Stop;

		if (Waves == null || Waves.Waves.Count == 0)
		{
			GD.PushWarning("WaveDirector: no WaveSet assigned.");
			return;
		}

		WaveNumber = 1;
		StartCountdown(Waves.FirstWaveDelay, isBreak: false);
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance != null)
			GameEvents.Instance.EnemyKilled -= OnEnemyKilled;
	}

	/// <summary>Ends the countdown or break early (the player is ready).</summary>
	public void SkipCountdown()
	{
		if (CurrentPhase is Phase.Countdown or Phase.Break)
			TimeRemaining = Mathf.Min(TimeRemaining, 3f);
	}

	public void Stop() => CurrentPhase = Phase.Stopped;

	/// <summary>
	/// Debug jump: skip ahead to a wave. With openTrader, start that wave's trader break
	/// (so the trader opens at once), otherwise a short countdown.
	/// </summary>
	public void JumpTo(int wave, bool openTrader, float countdown)
	{
		if (Waves == null)
			return;
		WaveNumber = Mathf.Max(wave, 1);
		StartCountdown(openTrader ? Waves.BreakDuration : countdown, isBreak: openTrader);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ready_up"))
			SkipCountdown();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		switch (CurrentPhase)
		{
			case Phase.Countdown:
			case Phase.Break:
				TimeRemaining -= dt;
				if (TimeRemaining <= 0f)
					StartWave();
				break;

			case Phase.Wave:
				UpdateWave(dt);
				break;
		}
	}

	private void StartCountdown(float seconds, bool isBreak)
	{
		CurrentPhase = isBreak ? Phase.Break : Phase.Countdown;
		TimeRemaining = seconds;
		if (isBreak)
			GameEvents.Instance.EmitSignal(GameEvents.SignalName.BreakStarted, WaveNumber, seconds);
		GameEvents.Instance.EmitSignal(GameEvents.SignalName.WaveIncoming, WaveNumber, seconds);
	}

	private void StartWave()
	{
		BuildQueue(WaveNumber);
		CurrentPhase = Phase.Wave;
		_packTimer = 0f;
		GameEvents.Instance.EmitSignal(GameEvents.SignalName.WaveStarted, WaveNumber, _queuedCount);
	}

	private void BuildQueue(int waveNumber)
	{
		_queue.Clear();
		_pending.Clear();
		_ventBursts.Clear();
		_ventWaiting = 0;
		_queuedCount = 0;

		int index = Mathf.Min(waveNumber - 1, Waves.Waves.Count - 1);
		int extra = Mathf.Max(0, waveNumber - Waves.Waves.Count);
		WaveDefinition wave = Waves.Waves[index];
		float countScale = Mathf.Pow(Waves.EndlessCountMultiplier, extra);

		_maxAlive = Mathf.Min(wave.MaxAlive + Waves.EndlessMaxAliveGrowth * extra, Waves.MaxAliveCap);
		_packInterval = wave.PackInterval;

		foreach (WaveSpawn spawn in wave.Spawns)
		{
			if (spawn?.Enemy == null)
				continue;
			int remaining = Mathf.RoundToInt(spawn.Count * countScale);
			int packSize = Mathf.Max(1, spawn.PackSize);
			while (remaining > 0)
			{
				int count = Mathf.Min(packSize, remaining);
				remaining -= count;
				float order = Mathf.Lerp(spawn.EarliestFraction, 1f, GD.Randf());
				_queue.Add(new Pack { Scene = spawn.Enemy, Count = count, Order = order });
				_queuedCount += count;
			}
		}

		_queue.Sort((a, b) => a.Order.CompareTo(b.Order));
	}

	private void UpdateWave(float dt)
	{
		_cullTimer -= dt;
		if (_cullTimer <= 0f)
		{
			_cullTimer = 0.5f;
			CullFallen();
		}

		UpdateCamping(dt);
		UpdateVents(dt);

		_packTimer -= dt;
		if (_queue.Count > 0 && _packTimer <= 0f)
		{
			int room = _maxAlive - _alive.Count - _pending.Count - _ventWaiting;
			if (room > 0)
			{
				Pack pack = _queue[0];
				int count = Mathf.Min(pack.Count, room);
				SpawnPack(pack.Scene, count);
				if (count == pack.Count)
					_queue.RemoveAt(0);
				else
					_queue[0] = pack with { Count = pack.Count - count };
				_packTimer = _packInterval;
			}
		}

		SpawnPending();

		if (_queue.Count == 0 && _pending.Count == 0 && _ventWaiting == 0 && _alive.Count == 0)
			ClearWave();
	}

	private void ClearWave()
	{
		GameEvents.Instance.EmitSignal(GameEvents.SignalName.WaveCleared, WaveNumber);
		WaveNumber++;
		StartCountdown(Waves.BreakDuration, isBreak: true);
	}

	/// <summary>Tracks whether the player has stayed in roughly one place for a while.</summary>
	private void UpdateCamping(float dt)
	{
		if (!IsInstanceValid(_player))
			return;
		Vector3 at = _player.GlobalPosition;
		if (at.DistanceSquaredTo(_campAnchor) > CampRadius * CampRadius)
		{
			_campAnchor = at;
			_campTimer = 0f;
		}
		else
		{
			_campTimer += dt;
		}
	}

	/// <summary>A random usable vent near the player, or null.</summary>
	private Node3D PickVent()
	{
		if (!IsInstanceValid(_player))
			return null;
		double now = Time.GetTicksMsec() / 1000.0;
		_ventCandidates.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("enemy_vent"))
		{
			if (node is not Node3D vent)
				continue;
			if (_ventReadyAt.TryGetValue(vent, out double readyAt) && now < readyAt)
				continue;
			float distance = vent.GlobalPosition.DistanceTo(_player.GlobalPosition);
			if (distance >= VentMinDistance && distance <= VentMaxDistance)
				_ventCandidates.Add(vent);
		}
		return _ventCandidates.Count > 0 ? _ventCandidates[GD.RandRange(0, _ventCandidates.Count - 1)] : null;
	}

	/// <summary>Rattles each waiting vent, then lets its enemies out.</summary>
	private void UpdateVents(float dt)
	{
		for (int i = _ventBursts.Count - 1; i >= 0; i--)
		{
			VentBurst burst = _ventBursts[i];
			burst.Timer -= dt;
			burst.NextRattle -= dt;
			Vector3 at = burst.Vent.GlobalPosition;
			if (burst.NextRattle <= 0f && burst.Timer > 0f)
			{
				burst.NextRattle = 0.12f;
				ImpactEffects.SpawnSparks(GetTree().CurrentScene, at + Vector3.Up * 0.05f, Vector3.Up, VentSparkColor, 16);
			}
			if (burst.Timer > 0f)
				continue;

			_ventBursts.RemoveAt(i);
			_ventWaiting -= burst.Count;
			ImpactEffects.SpawnSparks(GetTree().CurrentScene, at + Vector3.Up * 0.1f, Vector3.Up, VentSparkColor, 40);
			for (int k = 0; k < burst.Count; k++)
			{
				float angle = GD.Randf() * Mathf.Tau;
				_pending.Enqueue(new PendingSpawn
				{
					Scene = burst.Scene,
					Position = NavGrid.SnapToGround(at + new Vector3(Mathf.Cos(angle) * 0.6f, 0.1f, Mathf.Sin(angle) * 0.6f)),
				});
			}
		}
	}

	/// <summary>Releases a pack: picks its spawn point and queues its enemies for creation.</summary>
	private void SpawnPack(PackedScene scene, int count)
	{
		float ventChance = PlayerCamping ? CampingVentChance : VentChance;
		if (GD.Randf() < ventChance && PickVent() is { } vent)
		{
			int fromVent = Mathf.Min(count, Mathf.Max(VentPackMax, 1));
			_ventReadyAt[vent] = Time.GetTicksMsec() / 1000.0 + VentCooldown;
			_ventBursts.Add(new VentBurst { Vent = vent, Scene = scene, Count = fromVent, Timer = VentWarning });
			_ventWaiting += fromVent;
			PlaceholderSfx.PlayAt(GetTree().CurrentScene, PlaceholderSfx.VentRattle, vent.GlobalPosition, 2f, 0.05f);
			EmitSignal(SignalName.VentOpened, vent, fromVent);
			count -= fromVent;
			if (count <= 0)
				return;
		}

		Node3D point = PickSpawnPoint();
		if (point == null)
		{
			// Nowhere to put them: drop the pack rather than stall the wave.
			_queuedCount -= count;
			return;
		}

		Vector3 centre = point.GlobalPosition;
		float radius = ScatterRadius * Mathf.Sqrt(Mathf.Max(count, 1) / 4f);
		for (int i = 0; i < count; i++)
		{
			float angle = GD.Randf() * Mathf.Tau;
			float r = Mathf.Sqrt(GD.Randf()) * radius;
			_pending.Enqueue(new PendingSpawn
			{
				Scene = scene,
				// A big pack scatters wide: keep everyone inside the walls and out of cover.
				Position = NavGrid.SnapToGround(centre + new Vector3(Mathf.Cos(angle) * r, 0.1f, Mathf.Sin(angle) * r)),
			});
		}
	}

	/// <summary>Creates a few of the queued enemies each tick.</summary>
	private void SpawnPending()
	{
		if (_pending.Count == 0)
			return;

		Node parent = GetTree().CurrentScene;
		for (int i = Mathf.Max(MaxSpawnsPerTick, 1); i > 0 && _pending.Count > 0; i--)
		{
			PendingSpawn spawn = _pending.Dequeue();
			var enemy = spawn.Scene.Instantiate<Node3D>();
			if (enemy is Enemy body)
				body.MakeChunky(Waves.HealthMultiplierFor(WaveNumber), Waves.WidthFor(WaveNumber));
			// Place it before it enters the tree, so it never spends a frame at the origin.
			enemy.Position = spawn.Position;
			parent.AddChild(enemy);
			enemy.GlobalPosition = spawn.Position;
			_alive.Add(enemy);
			_queuedCount--;
			enemy.TreeExiting += () => _alive.Remove(enemy);
		}
	}

	private Node3D PickSpawnPoint()
	{
		var points = GetTree().GetNodesInGroup("enemy_spawn");
		if (points.Count == 0)
			return null;

		List<Node3D> far = _farPoints;
		far.Clear();
		Node3D farthest = null;
		float farthestDistance = -1f;
		foreach (Node node in points)
		{
			if (node is not Node3D point)
				continue;
			float distance = IsInstanceValid(_player) ? point.GlobalPosition.DistanceTo(_player.GlobalPosition) : 0f;
			if (distance >= MinSpawnDistance)
				far.Add(point);
			if (distance > farthestDistance)
			{
				farthestDistance = distance;
				farthest = point;
			}
		}

		return far.Count > 0 ? far[GD.RandRange(0, far.Count - 1)] : farthest;
	}

	private void OnEnemyKilled(Node3D enemy) => _alive.Remove(enemy);

	private void CullFallen()
	{
		_fallen.Clear();
		foreach (Node3D enemy in _alive)
		{
			if (!IsInstanceValid(enemy) || enemy.GlobalPosition.Y < KillHeight)
				_fallen.Add(enemy);
		}
		foreach (Node3D enemy in _fallen)
		{
			_alive.Remove(enemy);
			if (IsInstanceValid(enemy))
				enemy.QueueFree();
		}
	}
}
