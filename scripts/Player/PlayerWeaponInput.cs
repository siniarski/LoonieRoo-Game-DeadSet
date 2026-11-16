using System.Collections.Generic;
using Deadset.Weapons;
using Godot;

namespace Deadset.Player;

/// <summary>
/// Turns player input into weapon commands (trigger, reload) and feeds weapon recoil
/// to the camera. Knows nothing about how the equipped weapon actually fires.
/// Also holds the weapons the player owns (one per slot) and switches between them:
/// 1/2/3 or the mouse wheel.
/// </summary>
public partial class PlayerWeaponInput : Node
{
	[Signal] public delegate void WeaponEquippedEventHandler(Weapon weapon);
	/// <summary>A weapon was added to the inventory.</summary>
	[Signal] public delegate void WeaponAddedEventHandler(Weapon weapon);

	[Export] public NodePath StartingWeaponPath { get; set; } = "../Head/Camera3D/NailGun";
	[Export] public NodePath CameraPath { get; set; } = "../Head/Camera3D";
	/// <summary>Who the player is. Brings the hero's always-on melee.</summary>
	[Export] public HeroDefinition Hero { get; set; }

	/// <summary>The hero's always-on melee (not in a slot). Null if the hero has none.</summary>
	public QuickMeleeWeapon QuickMelee { get; private set; }
	/// <summary>Melee key held. Also set by the sim bot.</summary>
	public bool MeleeHeld { get; set; }
	private bool _fireHeld;

	public Weapon EquippedWeapon { get; private set; }
	/// <summary>Owned weapons in the order they were acquired (slot 1 first).</summary>
	public IReadOnlyList<Weapon> Weapons => _weapons;
	/// <summary>Player-upgrade modifiers that apply to every weapon carried, including ones bought later.</summary>
	public WeaponModifiers SharedMods { get; } = new();

	private readonly List<Weapon> _weapons = new();

	private CameraRecoil _camera;
	private PlayerController _player;
	private bool _wasMouseCaptured;

	public override void _Ready()
	{
		_camera = GetNode<CameraRecoil>(CameraPath);
		_player = GetParent<PlayerController>();
		Equip(AddWeapon(GetNode<Weapon>(StartingWeaponPath)));
		AddQuickMelee();
	}

	private void AddQuickMelee()
	{
		if (Hero?.QuickMelee?.Instantiate() is not QuickMeleeWeapon melee)
			return;
		QuickMelee = melee;
		melee.Name = "QuickMelee";
		melee.SharedMods = SharedMods;
		melee.AimSource = _camera;
		melee.Wielder = _player;
		_camera.AddChild(melee);
		melee.Fired += OnWeaponFired;
		melee.HitLanded += (killed, _) =>
		{
			if (killed && melee.RefundOnKill > 0 && melee.RefundStats != null)
				FindByStats(melee.RefundStats)?.AddReserveAmmo(melee.RefundOnKill);
		};
	}

	/// <summary>True if a weapon using these stats is already owned.</summary>
	public bool Owns(WeaponStats stats) => FindByStats(stats) != null;

	public Weapon FindByStats(WeaponStats stats)
	{
		foreach (Weapon weapon in _weapons)
			if (weapon.Stats == stats)
				return weapon;
		return null;
	}

	/// <summary>Creates a weapon from its scene, puts it in the player's hands and equips it.</summary>
	public Weapon Give(PackedScene scene)
	{
		var weapon = scene.Instantiate<Weapon>();
		if (Owns(weapon.Stats))
		{
			weapon.Free();
			return null;
		}
		GetNode(CameraPath).AddChild(weapon);
		Equip(AddWeapon(weapon));
		return weapon;
	}

	private Weapon AddWeapon(Weapon weapon)
	{
		weapon.SharedMods = SharedMods;
		weapon.AimSource = _camera;
		weapon.Wielder = _player;
		_weapons.Add(weapon);
		SetActive(weapon, false);
		EmitSignal(SignalName.WeaponAdded, weapon);
		return weapon;
	}

	private static void SetActive(Weapon weapon, bool active)
	{
		weapon.Visible = active;
		weapon.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
	}

	/// <summary>Switches by slot (0-based). Ignored for empty slots.</summary>
	public void EquipSlot(int slot)
	{
		if (slot >= 0 && slot < _weapons.Count)
			Equip(_weapons[slot]);
	}

	private void Cycle(int step)
	{
		if (_weapons.Count < 2 || EquippedWeapon == null)
			return;
		int index = _weapons.IndexOf(EquippedWeapon);
		Equip(_weapons[(index + step + _weapons.Count) % _weapons.Count]);
	}

	public void Equip(Weapon weapon)
	{
		if (weapon == null || weapon == EquippedWeapon)
			return;
		if (EquippedWeapon != null)
		{
			EquippedWeapon.Holster();
			EquippedWeapon.Fired -= OnWeaponFired;
			SetActive(EquippedWeapon, false);
		}

		EquippedWeapon = weapon;
		SetActive(weapon, true);
		weapon.AimSource = _camera;
		weapon.Wielder = _player;
		weapon.Fired += OnWeaponFired;
		EmitSignal(SignalName.WeaponEquipped, weapon);
	}

	private void OnWeaponFired(float recoilPitchDegrees, float recoilYawDegrees)
	{
		_camera.AddKick(recoilPitchDegrees, recoilYawDegrees);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (EquippedWeapon == null)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false, CtrlPressed: false } key
			&& key.PhysicalKeycode is >= Key.Key1 and <= Key.Key9)
		{
			EquipSlot((int)(key.PhysicalKeycode - Key.Key1));
		}
		else if (@event is InputEventMouseButton { Pressed: true } wheel
			&& wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown
			&& Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			Cycle(wheel.ButtonIndex == MouseButton.WheelDown ? 1 : -1);
		}
		else if (@event.IsActionPressed("fire"))
		{
			// Ignore the click that recaptures the mouse.
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
			{
				_fireHeld = true;
				EquippedWeapon.SetTriggerHeld(true);
			}
		}
		else if (@event.IsActionReleased("fire"))
		{
			_fireHeld = false;
			EquippedWeapon.SetTriggerHeld(false);
		}
		else if (@event.IsActionPressed("melee"))
		{
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
				MeleeHeld = true;
		}
		else if (@event.IsActionReleased("melee"))
		{
			MeleeHeld = false;
		}
		else if (@event.IsActionPressed("aim"))
		{
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
				EquippedWeapon.SetAimHeld(true);
		}
		else if (@event.IsActionReleased("aim"))
		{
			EquippedWeapon.SetAimHeld(false);
		}
		else if (@event.IsActionPressed("reload"))
		{
			EquippedWeapon.RequestReload();
		}
	}

	public override void _Process(double delta)
	{
		if (EquippedWeapon == null)
			return;

		// Releasing the mouse (Esc) also lets go of the trigger and the sights.
		bool captured = Input.MouseMode == Input.MouseModeEnum.Captured;
		if (_wasMouseCaptured && !captured)
		{
			_fireHeld = false;
			MeleeHeld = false;
			EquippedWeapon.SetTriggerHeld(false);
			EquippedWeapon.SetAimHeld(false);
		}
		_wasMouseCaptured = captured;

		if (QuickMelee != null)
		{
			// The melee key, or the trigger once the gun in hand is completely dry.
			QuickMelee.SetTriggerHeld(MeleeHeld || (_fireHeld && EquippedWeapon.IsDry));
			bool swinging = QuickMelee.IsSwinging;
			EquippedWeapon.Blocked = swinging;
			EquippedWeapon.Visible = !swinging;
		}

		_player.IsAiming = EquippedWeapon.IsAiming;
		_player.AimFov = EquippedWeapon.Stats.AdsFov;
	}
}
