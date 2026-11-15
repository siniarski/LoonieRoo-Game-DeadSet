using Godot;

namespace Deadset.Core;

/// <summary>
/// Global game-flow events (autoload). Systems announce things here instead of
/// holding references to each other.
/// </summary>
public partial class GameEvents : Node
{
	public static GameEvents Instance { get; private set; }

	[Signal] public delegate void EnemyKilledEventHandler(Node3D enemy);

	/// <summary>Countdown before a wave (the first one, or after a break) has begun.</summary>
	[Signal] public delegate void WaveIncomingEventHandler(int waveNumber, float seconds);
	[Signal] public delegate void WaveStartedEventHandler(int waveNumber, int enemyCount);
	[Signal] public delegate void WaveClearedEventHandler(int waveNumber);
	/// <summary>The trader break between waves has begun.</summary>
	[Signal] public delegate void BreakStartedEventHandler(int nextWaveNumber, float seconds);

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}
}
