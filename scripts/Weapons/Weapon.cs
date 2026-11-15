using Godot;

namespace Deadset.Weapons;

/// <summary>
/// Base for every weapon. Handles trigger, fire rate, ammo and reloading.
/// Subclasses only decide what a shot does (projectile, hitscan, cone, melee...).
/// The wielder just pulls the trigger and asks for reloads; it never knows how the gun works.
/// </summary>
public abstract partial class Weapon : Node3D
{
	/// <summary>A shot went off. Recoil values are for whoever owns the camera.</summary>
	[Signal] public delegate void FiredEventHandler(float recoilPitchDegrees, float recoilYawDegrees);
	[Signal] public delegate void AmmoChangedEventHandler(int inMagazine, int reserve);
	[Signal] public delegate void ReloadStartedEventHandler(float duration);
	[Signal] public delegate void ReloadFinishedEventHandler();
	[Signal] public delegate void DryFiredEventHandler();
	/// <summary>One of this weapon's shots damaged something.</summary>
	[Signal] public delegate void HitLandedEventHandler(bool killed, bool weakSpot);

	[Export] public WeaponStats Stats { get; set; }

	public int AmmoInMagazine { get; private set; }
	public int ReserveAmmo { get; private set; }
	public bool IsReloading { get; private set; }
	/// <summary>0 at reload start, 1 when done. 0 when not reloading.</summary>
	public float ReloadProgress => IsReloading ? 1f - _reloadTimer / _reloadDuration : 0f;
	/// <summary>How many more rounds fit in the reserve.</summary>
	public int ReserveSpace => Stats.UsesAmmo ? Mathf.Max(0, Stats.MaxReserve - ReserveAmmo) : 0;

	// ---- Modifiers. The shared Stats resource is never changed. ----

	/// <summary>This weapon's own modifiers, from mods bought for it.</summary>
	public WeaponModifiers OwnMods { get; } = new();
	/// <summary>Modifiers shared by every weapon the player carries (player upgrades). Set by the wielder.</summary>
	public WeaponModifiers SharedMods { get; set; } = new();

	public float ReloadTimeMultiplier => OwnMods.ReloadTimeMultiplier * SharedMods.ReloadTimeMultiplier;
	public float FireRateMultiplier => OwnMods.FireRateMultiplier * SharedMods.FireRateMultiplier;
	public float DamageMultiplier => OwnMods.DamageMultiplier * SharedMods.DamageMultiplier;
	public int ExtraRicochets => OwnMods.Ricochets + SharedMods.Ricochets;
	public int Pierce => Stats.BasePierce + OwnMods.Pierce + SharedMods.Pierce;
	public float PierceDamageKeep =>
		Mathf.Clamp(Stats.BasePierceDamageKeep + OwnMods.PierceDamageKeep + SharedMods.PierceDamageKeep, 0f, 1f);
	public int Chain => OwnMods.Chain + SharedMods.Chain;
	public float ReachMultiplier => OwnMods.ReachMultiplier * SharedMods.ReachMultiplier;
	public int ExtraTargets => OwnMods.ExtraTargets + SharedMods.ExtraTargets;
	public int MagazineSize =>
		Mathf.Max(1, Mathf.RoundToInt(Stats.MagazineSize * OwnMods.MagazineMultiplier * SharedMods.MagazineMultiplier));

	private readonly System.Collections.Generic.Dictionary<WeaponMod, int> _modLevels = new();

	public int LevelOf(WeaponMod mod) => mod != null && _modLevels.TryGetValue(mod, out int level) ? level : 0;

	/// <summary>Applies one more level of a mod. Returns false if it is already maxed.</summary>
	public bool AddModLevel(WeaponMod mod)
	{
		int level = LevelOf(mod);
		if (mod == null || level >= mod.MaxLevel)
			return false;
		_modLevels[mod] = level + 1;
		int magazineBefore = MagazineSize;
		mod.Apply(OwnMods);
		// A bigger magazine comes loaded.
		if (Stats.UsesAmmo && MagazineSize > magazineBefore)
			AmmoInMagazine += MagazineSize - magazineBefore;
		EmitSignal(SignalName.AmmoChanged, AmmoInMagazine, ReserveAmmo);
		return true;
	}

	/// <summary>Reload time after modifiers, in seconds.</summary>
	public float EffectiveReloadTime => Stats.ReloadTime * ReloadTimeMultiplier;

	/// <summary>True while aim is held and the weapon can aim (not reloading).</summary>
	public bool IsAiming => _aimHeld && !IsReloading;
	/// <summary>0 = hip, 1 = fully aimed down sights. Eases over Stats.AdsTime.</summary>
	public float AimBlend { get; private set; }

	/// <summary>Current spread, blended between hip and aimed.</summary>
	protected float CurrentSpreadDegrees => Mathf.Lerp(Stats.SpreadDegrees, Stats.AdsSpreadDegrees, AimBlend);

	/// <summary>Where shots aim from (normally the player's camera). Set by the wielder.</summary>
	public Node3D AimSource { get; set; }
	/// <summary>The body holding this weapon, so its own shots ignore it. Set by the wielder.</summary>
	public CollisionObject3D Wielder { get; set; }

	private bool _triggerHeld;
	private bool _triggerJustPressed;
	private bool _aimHeld;
	private float _cooldown;
	private float _reloadTimer;
	private float _reloadDuration = 1f;

	public override void _Ready()
	{
		AmmoInMagazine = Stats.UsesAmmo ? Stats.MagazineSize : 0;
		ReserveAmmo = Stats.UsesAmmo ? Stats.StartingReserve : 0;
	}

	public void SetTriggerHeld(bool held)
	{
		if (held && !_triggerHeld)
			_triggerJustPressed = true;
		_triggerHeld = held;
	}

	public void SetAimHeld(bool held) => _aimHeld = held;

	/// <summary>Put away: lets go of everything and cancels a reload in progress.</summary>
	public virtual void Holster()
	{
		_triggerHeld = false;
		_triggerJustPressed = false;
		_aimHeld = false;
		AimBlend = 0f;
		IsReloading = false;
	}

	public void RequestReload()
	{
		if (!Stats.UsesAmmo || IsReloading || AmmoInMagazine >= MagazineSize || ReserveAmmo <= 0)
			return;

		IsReloading = true;
		_reloadDuration = Mathf.Max(EffectiveReloadTime, 0.01f);
		_reloadTimer = _reloadDuration;
		EmitSignal(SignalName.ReloadStarted, _reloadDuration);
	}

	/// <summary>Adds rounds to the reserve, clamped to Stats.MaxReserve.</summary>
	public void AddReserveAmmo(int amount)
	{
		ReserveAmmo = Mathf.Clamp(ReserveAmmo + amount, 0, Stats.MaxReserve);
		EmitSignal(SignalName.AmmoChanged, AmmoInMagazine, ReserveAmmo);
	}

	/// <summary>Called by this weapon's projectiles when they damage something.</summary>
	public void ReportHit(bool killed, bool weakSpot = false) => EmitSignal(SignalName.HitLanded, killed, weakSpot);

	/// <summary>Perform one shot. Ammo and timing are already handled.</summary>
	protected abstract void FireShot();

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (IsReloading)
		{
			_reloadTimer -= dt;
			if (_reloadTimer <= 0f)
				FinishReload();
		}

		float aimRate = dt / Mathf.Max(Stats.AdsTime, 0.01f);
		AimBlend = Mathf.MoveToward(AimBlend, IsAiming ? 1f : 0f, aimRate);

		_cooldown -= dt;
		bool wantsToFire = Stats.Automatic ? _triggerHeld : _triggerJustPressed;

		if (wantsToFire && _cooldown <= 0f && !IsReloading)
		{
			if (!Stats.UsesAmmo || AmmoInMagazine > 0)
				Shoot(dt);
			else if (ReserveAmmo > 0)
				RequestReload();
			else if (_triggerJustPressed)
				EmitSignal(SignalName.DryFired);
		}

		_triggerJustPressed = false;
	}

	private void Shoot(float dt)
	{
		if (Stats.UsesAmmo)
			AmmoInMagazine--;
		FireShot();

		float recoilScale = Mathf.Lerp(1f, Stats.AdsRecoilMultiplier, AimBlend);
		float yaw = (float)GD.RandRange(-Stats.RecoilYawJitterDegrees, Stats.RecoilYawJitterDegrees);
		EmitSignal(SignalName.Fired, Stats.RecoilPitchDegrees * recoilScale, yaw * recoilScale);
		EmitSignal(SignalName.AmmoChanged, AmmoInMagazine, ReserveAmmo);

		// Carry over at most one frame of leftover time so the fire rate stays accurate
		// without letting a long idle period bank up a burst of instant shots.
		_cooldown = Mathf.Max(_cooldown, -dt) + 1f / (Stats.FireRate * FireRateMultiplier);
	}

	private void FinishReload()
	{
		int taken = Mathf.Min(MagazineSize - AmmoInMagazine, ReserveAmmo);
		AmmoInMagazine += taken;
		ReserveAmmo -= taken;
		IsReloading = false;
		EmitSignal(SignalName.ReloadFinished);
		EmitSignal(SignalName.AmmoChanged, AmmoInMagazine, ReserveAmmo);
	}
}
