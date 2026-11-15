namespace Deadset.Weapons;

/// <summary>
/// Numbers that change how a weapon performs. Each weapon has its own set (from mods bought
/// for it) and shares one set with every weapon the player carries (from player upgrades).
/// The weapon multiplies or adds the two together; the WeaponStats resource is never changed.
/// </summary>
public sealed class WeaponModifiers
{
	/// <summary>Scales reload time. Below 1 = faster reloads.</summary>
	public float ReloadTimeMultiplier { get; set; } = 1f;
	/// <summary>Scales shots (or swings) per second.</summary>
	public float FireRateMultiplier { get; set; } = 1f;
	/// <summary>Scales damage per hit.</summary>
	public float DamageMultiplier { get; set; } = 1f;
	/// <summary>Scales magazine size (rounded).</summary>
	public float MagazineMultiplier { get; set; } = 1f;
	/// <summary>Bounces off walls before sticking.</summary>
	public int Ricochets { get; set; }
	/// <summary>Extra enemies each shot passes through.</summary>
	public int Pierce { get; set; }
	/// <summary>Added to the share of damage a shot keeps after passing through an enemy.</summary>
	public float PierceDamageKeep { get; set; }
	/// <summary>Times a shot jumps from the enemy it hit to another one nearby.</summary>
	public int Chain { get; set; }
	/// <summary>Scales melee reach and cone width.</summary>
	public float ReachMultiplier { get; set; } = 1f;
	/// <summary>Extra enemies one melee swing can hit.</summary>
	public int ExtraTargets { get; set; }
}
