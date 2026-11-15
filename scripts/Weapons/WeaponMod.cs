using Godot;

namespace Deadset.Weapons;

/// <summary>Which weapon number a WeaponMod changes, and how Value is read.</summary>
public enum WeaponModStat
{
	/// <summary>Adds Value (whole number) enemies each shot passes through.</summary>
	Pierce,
	/// <summary>Adds Value to the share of damage kept after each pierce (0.15 = +15%).</summary>
	PierceDamageKeep,
	/// <summary>Adds Value (whole number) jumps from a hit enemy to another nearby.</summary>
	Chain,
	/// <summary>Multiplies magazine size by Value.</summary>
	Magazine,
	/// <summary>Multiplies fire rate (or swing rate) by Value.</summary>
	FireRate,
	/// <summary>Multiplies reload time by Value (below 1 = faster).</summary>
	ReloadTime,
	/// <summary>Multiplies damage by Value.</summary>
	Damage,
	/// <summary>Multiplies melee reach and cone width by Value.</summary>
	Reach,
	/// <summary>Adds Value (whole number) enemies per melee swing.</summary>
	ExtraTargets,
	/// <summary>Adds Value (whole number) wall bounces.</summary>
	Ricochets,
	/// <summary>Adds Value to the chance per unit of ammo that it isn't used (0.2 = 20%).</summary>
	AmmoSave,
}

/// <summary>
/// A weapon upgrade sold in the trader for one weapon, as data. Each level bought applies
/// Value once more, so stackable mods just have MaxLevel above 1. Price rises per level.
/// </summary>
[GlobalClass]
public partial class WeaponMod : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "Mod";
	[Export] public string Description { get; set; } = "";
	[Export] public WeaponModStat Stat { get; set; }
	[Export] public float Value { get; set; } = 1f;
	[Export(PropertyHint.Range, "1,10")] public int MaxLevel { get; set; } = 1;
	/// <summary>Price of the first level.</summary>
	[Export] public int Price { get; set; } = 100;
	/// <summary>Each further level costs this much more than the last (1.5 = +50%).</summary>
	[Export] public float PriceGrowth { get; set; } = 1.5f;

	public int PriceForLevel(int level) => Mathf.RoundToInt(Price * Mathf.Pow(PriceGrowth, Mathf.Max(level - 1, 0)) / 5f) * 5;

	/// <summary>Applies one level of this mod.</summary>
	public void Apply(WeaponModifiers mods)
	{
		switch (Stat)
		{
			case WeaponModStat.Pierce: mods.Pierce += Mathf.RoundToInt(Value); break;
			case WeaponModStat.PierceDamageKeep: mods.PierceDamageKeep += Value; break;
			case WeaponModStat.Chain: mods.Chain += Mathf.RoundToInt(Value); break;
			case WeaponModStat.Magazine: mods.MagazineMultiplier *= Value; break;
			case WeaponModStat.FireRate: mods.FireRateMultiplier *= Value; break;
			case WeaponModStat.ReloadTime: mods.ReloadTimeMultiplier *= Value; break;
			case WeaponModStat.Damage: mods.DamageMultiplier *= Value; break;
			case WeaponModStat.Reach: mods.ReachMultiplier *= Value; break;
			case WeaponModStat.ExtraTargets: mods.ExtraTargets += Mathf.RoundToInt(Value); break;
			case WeaponModStat.Ricochets: mods.Ricochets += Mathf.RoundToInt(Value); break;
			case WeaponModStat.AmmoSave: mods.AmmoSaveChance += Value; break;
		}
	}
}
