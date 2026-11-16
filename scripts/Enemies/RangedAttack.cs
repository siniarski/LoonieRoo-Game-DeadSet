using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// A variant's ranged attack. Ranged enemies are rare, stop to shoot (so they're easy targets
/// while they do), show a glowing tell before every shot, and throw slow arcs you can dodge.
/// At most RangedSlots.Max of them shoot at once.
/// </summary>
[GlobalClass]
public partial class RangedAttack : Resource
{
	public enum AttackKind
	{
		/// <summary>One lobbed blob in an arc (mortar cube, spit).</summary>
		Lob,
		/// <summary>A low spray of small stones (a kangaroo's back-foot flick).</summary>
		Spread,
		/// <summary>Dropped from above while flying over (a magpie's stones). Doesn't stop to throw.</summary>
		Drop,
		/// <summary>No projectile: a call that hurries every enemy nearby (a howler dingo).</summary>
		Howl,
	}

	[Export] public AttackKind Kind { get; set; } = AttackKind.Lob;
	[Export] public float MinRange { get; set; } = 7f;
	[Export] public float MaxRange { get; set; } = 20f;
	/// <summary>Seconds of glowing tell before the shot.</summary>
	[Export] public float Windup { get; set; } = 0.8f;
	[Export] public float Cooldown { get; set; } = 4f;
	[Export] public float FirstDelay { get; set; } = 1.5f;
	[Export] public float Damage { get; set; } = 12f;
	/// <summary>Seconds the projectile is in the air (longer = higher, slower arc).</summary>
	[Export] public float FlightTime { get; set; } = 1.2f;
	[Export] public float SplashRadius { get; set; } = 1.6f;
	/// <summary>A puddle left where it lands, hurting anyone standing in it (0 = none).</summary>
	[Export] public float PuddleSeconds { get; set; }
	[Export] public float PuddleDamagePerSecond { get; set; } = 6f;
	/// <summary>Shows a ring where it will land.</summary>
	[Export] public bool LandingMarker { get; set; } = true;
	[Export] public int Count { get; set; } = 1;
	[Export] public float SpreadDegrees { get; set; } = 12f;
	[Export] public Color Color { get; set; } = new(0.5f, 1f, 0.4f);
	[Export] public float ProjectileSize { get; set; } = 0.3f;
	/// <summary>Projectile is a cube (wombat) rather than a ball.</summary>
	[Export] public bool Cube { get; set; }
	/// <summary>Where the shot leaves from, relative to the body (model space, facing -Z).</summary>
	[Export] public Vector3 LaunchOffset { get; set; } = new(0f, 1.2f, -0.4f);
	[Export] public EnemyCue Cue { get; set; } = EnemyCue.Grunt;
	[Export] public float CuePitch { get; set; } = 1f;
	/// <summary>Howl: how far it reaches and how long the hurry lasts.</summary>
	[Export] public float HowlRadius { get; set; } = 30f;
	[Export] public float HowlSeconds { get; set; } = 4f;
	[Export] public float HowlSpeedMultiplier { get; set; } = 1.35f;
}
