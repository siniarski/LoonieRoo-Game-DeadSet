using Godot;

namespace Deadset.Upgrades;

/// <summary>Take less damage while health is below a threshold (Smoko Shield).</summary>
[GlobalClass]
public partial class LowHealthResistanceUpgrade : UpgradeDefinition
{
	/// <summary>Health fraction below which the resistance kicks in.</summary>
	[Export(PropertyHint.Range, "0,1")] public float HealthThreshold { get; set; } = 0.35f;
	/// <summary>Damage multiplier while below the threshold (0.6 = 40% less).</summary>
	[Export(PropertyHint.Range, "0,1")] public float DamageMultiplier { get; set; } = 0.6f;

	public override void Apply(UpgradeContext context, int stacks)
	{
		var health = context.Health;
		health.AddDamageModifier(amount =>
			health.Current < health.MaxHealth * HealthThreshold ? amount * DamageMultiplier : amount);
	}
}
