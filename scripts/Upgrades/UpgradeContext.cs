using Deadset.Combat;
using Deadset.Player;
using Deadset.Weapons;

namespace Deadset.Upgrades;

/// <summary>Everything an upgrade may touch on the player that received it.</summary>
public sealed class UpgradeContext
{
	public PlayerController Player { get; init; }
	public HealthComponent Health { get; init; }
	/// <summary>The weapon equipped when the upgrade was granted. May be null.</summary>
	public Weapon Weapon { get; init; }
	/// <summary>Subscribe to game events here so hooks end when the player does.</summary>
	public PlayerUpgrades Upgrades { get; init; }
}
