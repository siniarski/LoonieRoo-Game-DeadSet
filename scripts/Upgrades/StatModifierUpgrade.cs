using Godot;

namespace Deadset.Upgrades;

/// <summary>Which number a StatModifierUpgrade changes, and how Value is read.</summary>
public enum UpgradeStat
{
	/// <summary>Multiplies sprint speed by Value.</summary>
	SprintSpeed,
	/// <summary>Multiplies walk speed by Value.</summary>
	WalkSpeed,
	/// <summary>Multiplies reload time by Value (below 1 = faster).</summary>
	ReloadTime,
	/// <summary>Multiplies shots per second by Value.</summary>
	FireRate,
	/// <summary>Multiplies weapon damage by Value.</summary>
	WeaponDamage,
	/// <summary>Adds Value (whole number) ricochets to each shot.</summary>
	Ricochets,
	/// <summary>Unlocks the dash. Value is ignored.</summary>
	Dash,
}

/// <summary>Changes one player or weapon number. Each stack applies Value again.</summary>
[GlobalClass]
public partial class StatModifierUpgrade : UpgradeDefinition
{
	[Export] public UpgradeStat Stat { get; set; }
	/// <summary>A multiplier or an amount to add, depending on Stat.</summary>
	[Export] public float Value { get; set; } = 1f;

	public override void Apply(UpgradeContext context, int stacks)
	{
		switch (Stat)
		{
			case UpgradeStat.SprintSpeed:
				context.Player.SprintSpeedMultiplier *= Value;
				break;
			case UpgradeStat.WalkSpeed:
				context.Player.WalkSpeedMultiplier *= Value;
				break;
			case UpgradeStat.Dash:
				context.Player.DashUnlocked = true;
				break;
			case UpgradeStat.ReloadTime when context.WeaponMods != null:
				context.WeaponMods.ReloadTimeMultiplier *= Value;
				break;
			case UpgradeStat.FireRate when context.WeaponMods != null:
				context.WeaponMods.FireRateMultiplier *= Value;
				break;
			case UpgradeStat.WeaponDamage when context.WeaponMods != null:
				context.WeaponMods.DamageMultiplier *= Value;
				break;
			case UpgradeStat.Ricochets when context.WeaponMods != null:
				context.WeaponMods.Ricochets += Mathf.RoundToInt(Value);
				break;
		}
	}
}
