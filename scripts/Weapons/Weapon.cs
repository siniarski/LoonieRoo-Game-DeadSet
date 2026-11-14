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
	[Signal] public delegate void HitLandedEventHandler(bool killed);

	[Export] public WeaponStats Stats { get; set; }

	public int AmmoInMagazine { get; private set; }
	public int ReserveAmmo { get; private set; }
	public bool IsReloading { get; private set; }
	/// <summary>0 at reload start, 1 when done. 0 when not reloading.</summary>
	public float ReloadProgress => IsReloading ? 1f - _reloadTimer / Stats.ReloadTime : 0f;

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

	public override void _Ready()
	{
		AmmoInMagazine = Stats.MagazineSize;
		ReserveAmmo = Stats.StartingReserve;
	}

	public void SetTriggerHeld(bool held)
	{
		if (held && !_triggerHeld)
			_triggerJustPressed = true;
		_triggerHeld = held;
	}

	public void SetAimHeld(bool held) => _aimHeld = held;

	public void RequestReload()
	{
		if (IsReloading || AmmoInMagazine >= Stats.MagazineSize || ReserveAmmo <= 0)
			return;

		IsReloading = true;
		_reloadTimer = Stats.ReloadTime;
		EmitSignal(SignalName.ReloadStarted, Stats.ReloadTime);
	}

	/// <summary>Called by this weapon's projectiles when they damage something.</summary>
	public void ReportHit(bool killed) => EmitSignal(SignalName.HitLanded, killed);

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
			if (AmmoInMagazine > 0)
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
		AmmoInMagazine--;
		FireShot();

		float recoilScale = Mathf.Lerp(1f, Stats.AdsRecoilMultiplier, AimBlend);
		float yaw = (float)GD.RandRange(-Stats.RecoilYawJitterDegrees, Stats.RecoilYawJitterDegrees);
		EmitSignal(SignalName.Fired, Stats.RecoilPitchDegrees * recoilScale, yaw * recoilScale);
		EmitSignal(SignalName.AmmoChanged, AmmoInMagazine, ReserveAmmo);

		// Carry over at most one frame of leftover time so the fire rate stays accurate
		// without letting a long idle period bank up a burst of instant shots.
		_cooldown = Mathf.Max(_cooldown, -dt) + 1f / Stats.FireRate;
	}

	private void FinishReload()
	{
		int taken = Mathf.Min(Stats.MagazineSize - AmmoInMagazine, ReserveAmmo);
		AmmoInMagazine += taken;
		ReserveAmmo -= taken;
		IsReloading = false;
		EmitSignal(SignalName.ReloadFinished);
		EmitSignal(SignalName.AmmoChanged, AmmoInMagazine, ReserveAmmo);
	}
}
