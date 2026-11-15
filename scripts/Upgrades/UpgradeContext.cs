using Deadset.Combat;
using Deadset.Player;
using Deadset.Weapons;

namespace Deadset.Upgrades;

/// <summary>Everything an upgrade may touch on the player that received it.</summary>
public sealed class UpgradeContext
{
	public PlayerController Player { get; init; }
	public HealthComponent Health { get; init; }
	/// <summary>Modifiers shared by every weapon the player carries, now and later. May be null.</summary>
	public WeaponModifiers WeaponMods { get; init; }
	/// <summary>Subscribe to game events here so hooks end when the player does.</summary>
	public PlayerUpgrades Upgrades { get; init; }
}
