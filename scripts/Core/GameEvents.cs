using Godot;

namespace Deadset.Core;

/// <summary>
/// Global game-flow events (autoload). Systems announce things here instead of
/// holding references to each other. Waves and cash will listen to EnemyKilled.
/// </summary>
public partial class GameEvents : Node
{
	public static GameEvents Instance { get; private set; }

	[Signal] public delegate void EnemyKilledEventHandler(Node3D enemy);

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
