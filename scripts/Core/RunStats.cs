using Deadset.Combat;
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

	public override void _Ready()
	{
		AddToGroup("run_stats");
		GameEvents.Instance.EnemyKilled += OnEnemyKilled;
		GameEvents.Instance.WaveStarted += OnWaveStarted;
		GameEvents.Instance.WaveCleared += OnWaveCleared;

		HealthComponent health = HealthComponent.FindOn(GetTree().GetFirstNodeInGroup("player"));
		if (health != null)
			health.Died += EndRun;
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
		if (!Ended)
			Seconds += (float)delta;
	}

	private void OnEnemyKilled(Node3D enemy)
	{
		if (!Ended)
			Kills++;
	}

	private void OnWaveStarted(int wave, int count) => HighestWave = Mathf.Max(HighestWave, wave);

	private void OnWaveCleared(int wave) => WavesCleared = Mathf.Max(WavesCleared, wave);

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
