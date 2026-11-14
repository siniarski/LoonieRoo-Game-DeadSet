using Deadset.Weapons;
using Godot;

namespace Deadset.Player;

/// <summary>
/// Turns player input into weapon commands (trigger, reload) and feeds weapon recoil
/// to the camera. Knows nothing about how the equipped weapon actually fires.
/// </summary>
public partial class PlayerWeaponInput : Node
{
	[Signal] public delegate void WeaponEquippedEventHandler(Weapon weapon);

	[Export] public NodePath StartingWeaponPath { get; set; } = "../Head/Camera3D/NailGun";
	[Export] public NodePath CameraPath { get; set; } = "../Head/Camera3D";

	public Weapon EquippedWeapon { get; private set; }

	private CameraRecoil _camera;
	private PlayerController _player;
	private bool _wasMouseCaptured;

	public override void _Ready()
	{
		_camera = GetNode<CameraRecoil>(CameraPath);
		_player = GetParent<PlayerController>();
		Equip(GetNode<Weapon>(StartingWeaponPath));
	}

	public void Equip(Weapon weapon)
	{
		if (EquippedWeapon != null)
		{
			EquippedWeapon.SetTriggerHeld(false);
			EquippedWeapon.SetAimHeld(false);
			EquippedWeapon.Fired -= OnWeaponFired;
		}

		EquippedWeapon = weapon;
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

		if (@event.IsActionPressed("fire"))
		{
			// Ignore the click that recaptures the mouse.
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
				EquippedWeapon.SetTriggerHeld(true);
		}
		else if (@event.IsActionReleased("fire"))
		{
			EquippedWeapon.SetTriggerHeld(false);
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
			EquippedWeapon.SetTriggerHeld(false);
			EquippedWeapon.SetAimHeld(false);
		}
		_wasMouseCaptured = captured;

		_player.IsAiming = EquippedWeapon.IsAiming;
		_player.AimFov = EquippedWeapon.Stats.AdsFov;
	}
}
