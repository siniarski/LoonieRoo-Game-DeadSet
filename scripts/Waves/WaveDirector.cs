using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Core;
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

	[Export] public WaveSet Waves { get; set; }
	/// <summary>Spawn points closer than this to the player are skipped (if any others exist).</summary>
	[Export] public float MinSpawnDistance { get; set; } = 12f;
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

	public Phase CurrentPhase { get; private set; } = Phase.Stopped;
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

		_packTimer -= dt;
		if (_queue.Count > 0 && _packTimer <= 0f)
		{
			int room = _maxAlive - _alive.Count - _pending.Count;
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

		if (_queue.Count == 0 && _pending.Count == 0 && _alive.Count == 0)
			ClearWave();
	}

	private void ClearWave()
	{
		GameEvents.Instance.EmitSignal(GameEvents.SignalName.WaveCleared, WaveNumber);
		WaveNumber++;
		StartCountdown(Waves.BreakDuration, isBreak: true);
	}

	/// <summary>Releases a pack: picks its spawn point and queues its enemies for creation.</summary>
	private void SpawnPack(PackedScene scene, int count)
	{
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
