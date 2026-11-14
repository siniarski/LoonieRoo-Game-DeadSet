using Godot;

namespace Deadset.Weapons;

/// <summary>Tunable numbers for a weapon. Edit the .tres in the editor, not the code.</summary>
[GlobalClass]
public partial class WeaponStats : Resource
{
	[Export] public string DisplayName { get; set; } = "Weapon";

	[ExportGroup("Firing")]
	/// <summary>Hold to keep firing (true) or one shot per click (false).</summary>
	[Export] public bool Automatic { get; set; } = true;
	/// <summary>Shots per second.</summary>
	[Export] public float FireRate { get; set; } = 7f;
	[Export] public float Damage { get; set; } = 25f;
	/// <summary>Random cone half-angle per shot, in degrees.</summary>
	[Export] public float SpreadDegrees { get; set; } = 0.6f;

	[ExportGroup("Ammo")]
	[Export] public int MagazineSize { get; set; } = 24;
	[Export] public int StartingReserve { get; set; } = 120;
	[Export] public int MaxReserve { get; set; } = 240;
	/// <summary>Seconds from reload start until the magazine is refilled.</summary>
	[Export] public float ReloadTime { get; set; } = 1.4f;

	[ExportGroup("Projectile")]
	/// <summary>Metres per second.</summary>
	[Export] public float ProjectileSpeed { get; set; } = 70f;
	/// <summary>Metres a projectile travels before despawning.</summary>
	[Export] public float MaxRange { get; set; } = 150f;

	[ExportGroup("Aiming (iron sights)")]
	/// <summary>Camera field of view while aiming down sights.</summary>
	[Export(PropertyHint.Range, "30,90")] public float AdsFov { get; set; } = 60f;
	/// <summary>Seconds to go from hip to fully aimed.</summary>
	[Export] public float AdsTime { get; set; } = 0.15f;
	/// <summary>Spread while fully aimed, in degrees.</summary>
	[Export] public float AdsSpreadDegrees { get; set; } = 0.1f;
	/// <summary>Recoil multiplier while fully aimed (1 = same as hip).</summary>
	[Export(PropertyHint.Range, "0,1")] public float AdsRecoilMultiplier { get; set; } = 0.6f;

	[ExportGroup("Feel")]
	/// <summary>Camera kick upward per shot, in degrees.</summary>
	[Export] public float RecoilPitchDegrees { get; set; } = 1.4f;
	/// <summary>Random sideways camera kick per shot, in degrees (plus or minus).</summary>
	[Export] public float RecoilYawJitterDegrees { get; set; } = 0.5f;
	/// <summary>How far the gun model jolts back toward the camera per shot, in metres.</summary>
	[Export] public float ModelKickDistance { get; set; } = 0.05f;
	/// <summary>How far the gun model tips up per shot, in degrees.</summary>
	[Export] public float ModelKickDegrees { get; set; } = 6f;
}
