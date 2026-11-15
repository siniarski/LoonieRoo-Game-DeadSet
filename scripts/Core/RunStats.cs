using Deadset.Combat;
using Deadset.Economy;
using Deadset.Player;
using Deadset.Weapons;
using Godot;

namespace Deadset.Core;

/// <summary>
/// Tracks how the current run is going (kills, waves, time) and announces the end of the run
/// when the player dies. Game state only; the game-over screen reads it.
/// </summary>
public partial class RunStats : Node
{
	[Signal] public delegate void RunEndedEventHandler();

	public int Kills { get; private set; }
	public int WavesCleared { get; private set; }
	public int HighestWave { get; private set; }
	public float Seconds { get; private set; }
	public bool Ended { get; private set; }

	/// <summary>Numbers for one wave, for the break summary.</summary>
	public struct WaveSummary
	{
		public int Wave;
		public int Kills;
		public int Hits;
		public int Headshots;
		public int CashEarned;
		public float DamageTaken;
		public float Seconds;
		public float HeadshotPercent => Hits > 0 ? 100f * Headshots / Hits : 0f;
	}

	/// <summary>The most recently cleared wave.</summary>
	public WaveSummary LastWave { get; private set; }

	private WaveSummary _current;
	private bool _inWave;

	public override void _Ready()
	{
		AddToGroup("run_stats");
		GameEvents.Instance.EnemyKilled += OnEnemyKilled;
		GameEvents.Instance.WaveStarted += OnWaveStarted;
		GameEvents.Instance.WaveCleared += OnWaveCleared;

		Node player = GetTree().GetFirstNodeInGroup("player");
		HealthComponent health = HealthComponent.FindOn(player);
		if (health != null)
		{
			health.Died += EndRun;
			health.Damaged += (amount, _) => { if (_inWave) _current.DamageTaken += amount; };
		}
		Wallet wallet = Wallet.FindOn(player);
		if (wallet != null)
			wallet.CashChanged += (_, delta) => { if (_inWave && delta > 0) _current.CashEarned += delta; };
		var weaponInput = player?.GetNodeOrNull<PlayerWeaponInput>("WeaponInput");
		if (weaponInput != null)
		{
			weaponInput.WeaponEquipped += BindWeapon;
			CallDeferred(MethodName.BindEquipped, weaponInput);
		}
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance == null)
			return;
		GameEvents.Instance.EnemyKilled -= OnEnemyKilled;
		GameEvents.Instance.WaveStarted -= OnWaveStarted;
		GameEvents.Instance.WaveCleared -= OnWaveCleared;
	}

	public override void _Process(double delta)
	{
		if (Ended)
			return;
		Seconds += (float)delta;
		if (_inWave)
			_current.Seconds += (float)delta;
	}

	private Weapon _weapon;

	private void BindEquipped(PlayerWeaponInput input)
	{
		if (input.EquippedWeapon != null)
			BindWeapon(input.EquippedWeapon);
	}

	private void BindWeapon(Weapon weapon)
	{
		if (_weapon == weapon)
			return;
		if (_weapon != null)
			_weapon.HitLanded -= OnHitLanded;
		_weapon = weapon;
		_weapon.HitLanded += OnHitLanded;
	}

	private void OnHitLanded(bool killed, bool weakSpot)
	{
		if (!_inWave)
			return;
		_current.Hits++;
		if (weakSpot)
			_current.Headshots++;
	}

	private void OnEnemyKilled(Node3D enemy)
	{
		if (Ended)
			return;
		Kills++;
		if (_inWave)
			_current.Kills++;
	}

	private void OnWaveStarted(int wave, int count)
	{
		HighestWave = Mathf.Max(HighestWave, wave);
		_current = new WaveSummary { Wave = wave };
		_inWave = true;
	}

	private void OnWaveCleared(int wave)
	{
		WavesCleared = Mathf.Max(WavesCleared, wave);
		_inWave = false;
		LastWave = _current;
	}

	private void EndRun()
	{
		if (Ended)
			return;
		Ended = true;
		EmitSignal(SignalName.RunEnded);
	}

	/// <summary>Starts a fresh run by reloading the scene.</summary>
	public void Restart() => GetTree().ReloadCurrentScene();
}
