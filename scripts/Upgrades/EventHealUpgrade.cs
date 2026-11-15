using Godot;

namespace Deadset.Upgrades;

public enum UpgradeTrigger { WaveCleared, EnemyKilled }

/// <summary>Heals the player whenever a game event fires (Hot Chips and Gravy).</summary>
[GlobalClass]
public partial class EventHealUpgrade : UpgradeDefinition
{
	[Export] public UpgradeTrigger Trigger { get; set; } = UpgradeTrigger.WaveCleared;
	/// <summary>Health restored each time the trigger fires.</summary>
	[Export] public float HealAmount { get; set; } = 35f;

	public override void Apply(UpgradeContext context, int stacks)
	{
		var health = context.Health;
		switch (Trigger)
		{
			case UpgradeTrigger.WaveCleared:
				context.Upgrades.WaveCleared += _ => health.Heal(HealAmount);
				break;
			case UpgradeTrigger.EnemyKilled:
				context.Upgrades.EnemyKilled += _ => health.Heal(HealAmount);
				break;
		}
	}
}
