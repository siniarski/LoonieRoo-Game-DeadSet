using Deadset.Combat;
using Godot;

namespace Deadset.Debug;

/// <summary>
/// TEMPORARY (until the game-over screen in Milestone 7): when the player dies,
/// restore full health after a short delay so testing can continue.
/// </summary>
public partial class PlayerTestRespawn : Node
{
	[Export] public NodePath HealthPath { get; set; } = "../HealthComponent";
	[Export] public float RespawnDelay { get; set; } = 2f;

	private HealthComponent _health;

	public override void _Ready()
	{
		_health = GetNode<HealthComponent>(HealthPath);
		_health.Died += OnDied;
	}

	private void OnDied()
	{
		GetTree().CreateTimer(RespawnDelay).Timeout += _health.ResetHealth;
	}
}
