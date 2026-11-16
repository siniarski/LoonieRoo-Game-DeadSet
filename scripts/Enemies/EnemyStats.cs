using Godot;

namespace Deadset.Enemies;

/// <summary>Tunable numbers shared by every enemy type. One .tres per enemy type.</summary>
[GlobalClass]
public partial class EnemyStats : Resource
{
	[Export] public string DisplayName { get; set; } = "Enemy";
	[Export] public float MaxHealth { get; set; } = 20f;
	/// <summary>Cash paid to the player for the kill.</summary>
	[Export] public int Bounty { get; set; } = 10;

	[ExportGroup("Movement")]
	/// <summary>Chase speed in m/s. Player walks at 5.5 and sprints at 8.5.</summary>
	[Export] public float MoveSpeed { get; set; } = 7.5f;
	/// <summary>How quickly it reaches chase speed or turns (m/s²).</summary>
	[Export] public float Acceleration { get; set; } = 30f;
	/// <summary>Seconds between path updates toward the player.</summary>
	[Export] public float RepathInterval { get; set; } = 0.2f;
	/// <summary>Enemies closer than this push apart, so swarms spread out.</summary>
	[Export] public float SeparationRadius { get; set; } = 0.8f;
	/// <summary>How hard enemies push apart (m/s at zero distance).</summary>
	[Export] public float SeparationStrength { get; set; } = 4f;
	/// <summary>Can be picked as a flanker (paths round behind the player). Off for chargers.</summary>
	[Export] public bool CanFlank { get; set; } = true;

	[ExportGroup("Attack")]
	[Export] public float AttackDamage { get; set; } = 5f;
	/// <summary>Distance at which it starts an attack, in metres.</summary>
	[Export] public float AttackRange { get; set; } = 1.1f;
	/// <summary>Minimum seconds between attacks.</summary>
	[Export] public float AttackCooldown { get; set; } = 0.6f;
}
