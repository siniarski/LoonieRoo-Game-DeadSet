using Deadset.Effects;
using Godot;

namespace Deadset.Player;

/// <summary>
/// First-person movement and mouse look for The Tradie.
/// Owns movement only: weapons, health and upgrades live in their own components.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 4.95f;
	[Export] public float SprintSpeed { get; set; } = 7.65f;
	/// <summary>How fast you reach target speed on the ground (m/s²).</summary>
	[Export] public float GroundAcceleration { get; set; } = 60f;
	/// <summary>How fast you stop on the ground with no input (m/s²).</summary>
	[Export] public float GroundDeceleration { get; set; } = 45f;
	/// <summary>Steering in the air (m/s²). Lower = more committed jumps.</summary>
	[Export] public float AirAcceleration { get; set; } = 14f;

	/// <summary>Move speed multiplier while aiming down sights. Sprint is blocked while aiming.</summary>
	[Export(PropertyHint.Range, "0.1,1")] public float AimMoveSpeedMultiplier { get; set; } = 0.6f;

	[ExportGroup("Dash")]
	/// <summary>Horizontal speed during the dash burst (m/s).</summary>
	[Export] public float DashSpeed { get; set; } = 12.6f;
	/// <summary>Seconds the burst lasts.</summary>
	[Export] public float DashDuration { get; set; } = 0.18f;
	/// <summary>Seconds before the dash can be used again.</summary>
	[Export] public float DashCooldown { get; set; } = 2.5f;
	/// <summary>Degrees of FOV added at the start of a dash. It eases back out.</summary>
	[Export] public float DashFovPunch { get; set; } = 12f;
	/// <summary>How quickly the FOV punch fades. Higher = shorter.</summary>
	[Export] public float DashFovRecoverySpeed { get; set; } = 7f;
	[Export] public float DashVolumeDb { get; set; } = -6f;

	[ExportGroup("Jump")]
	/// <summary>Peak jump height in metres. 1.25 clears a 1 m crate.</summary>
	[Export] public float JumpHeight { get; set; } = 1.25f;
	/// <summary>Extra gravity while falling, for a snappier, less floaty arc.</summary>
	[Export] public float FallGravityMultiplier { get; set; } = 1.6f;
	/// <summary>Seconds after leaving a ledge during which jump still works.</summary>
	[Export] public float CoyoteTime { get; set; } = 0.12f;
	/// <summary>Seconds before landing that a jump press is remembered.</summary>
	[Export] public float JumpBufferTime { get; set; } = 0.12f;

	[ExportGroup("Look")]
	/// <summary>Radians of rotation per pixel of mouse movement.</summary>
	[Export] public float MouseSensitivity { get; set; } = 0.0022f;
	[Export(PropertyHint.Range, "45,90")] public float MaxPitchDegrees { get; set; } = 88f;
	/// <summary>Scale mouse sensitivity with zoom so aiming feels the same on screen.</summary>
	[Export] public bool ScaleSensitivityWithFov { get; set; } = true;

	[ExportGroup("Camera")]
	[Export(PropertyHint.Range, "50,120")] public float BaseFov { get; set; } = 80f;
	[Export(PropertyHint.Range, "50,130")] public float SprintFov { get; set; } = 88f;
	/// <summary>How quickly the FOV eases toward its target. Higher = snappier.</summary>
	[Export] public float FovLerpSpeed { get; set; } = 8f;
	/// <summary>FOV ease speed when zooming in or out of the sights. Higher = snappier.</summary>
	[Export] public float AimFovLerpSpeed { get; set; } = 18f;

	/// <summary>A dash just started. Cooldown is how long until the next one.</summary>
	[Signal] public delegate void DashedEventHandler(float cooldown);

	// Runtime modifiers. Set from outside (upgrades); the exported base numbers never change.
	public float WalkSpeedMultiplier { get; set; } = 1f;
	public float SprintSpeedMultiplier { get; set; } = 1f;
	/// <summary>The dash does nothing until something unlocks it.</summary>
	public bool DashUnlocked { get; set; }

	public bool IsDashing => _dashTimer > 0f;
	/// <summary>Seconds until the dash is ready again (0 = ready).</summary>
	public float DashCooldownRemaining => Mathf.Max(_dashCooldownTimer, 0f);

	/// <summary>True while sprint is held and the player is moving forward.</summary>
	public bool IsSprinting { get; private set; }

	/// <summary>Set by whatever handles weapons. The player only applies the movement and camera side.</summary>
	public bool IsAiming { get; set; }
	/// <summary>FOV to zoom to while aiming. Set alongside IsAiming.</summary>
	public float AimFov { get; set; } = 60f;

	private Node3D _head;
	private Camera3D _camera;
	private float _gravity;
	private float _coyoteTimer;
	private float _jumpBufferTimer;
	private float _fov;
	private float _fovPunch;
	private float _dashTimer;
	private float _dashCooldownTimer;
	private Vector3 _dashVelocity;
	private AudioStreamPlayer _dashSfx;

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head");
		_camera = GetNode<Camera3D>("Head/Camera3D");
		_camera.Fov = BaseFov;
		_fov = BaseFov;
		_dashSfx = new AudioStreamPlayer { Stream = PlaceholderSfx.DashWhoosh, VolumeDb = DashVolumeDb };
		AddChild(_dashSfx);
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	/// <summary>True while a screen in the "modal_ui" group (the trader) is showing.</summary>
	private bool IsMenuOpen()
	{
		foreach (Node node in GetTree().GetNodesInGroup("modal_ui"))
			if (node is CanvasItem { Visible: true } item && item.IsVisibleInTree())
				return true;
		return false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("release_mouse"))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (Input.MouseMode != Input.MouseModeEnum.Captured)
		{
			// Click back into the window to resume. Swallow the click so it never fires a weapon.
			// Only real clicks (not the scroll wheel), and never while a menu is up.
			if (@event is InputEventMouseButton { Pressed: true } click
				&& click.ButtonIndex is MouseButton.Left or MouseButton.Right
				&& !IsMenuOpen())
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (@event is InputEventMouseMotion motion)
		{
			float sensitivity = MouseSensitivity;
			if (ScaleSensitivityWithFov)
				sensitivity *= _fov / BaseFov;

			RotateY(-motion.Relative.X * sensitivity);
			float maxPitch = Mathf.DegToRad(MaxPitchDegrees);
			Vector3 headRot = _head.Rotation;
			headRot.X = Mathf.Clamp(headRot.X - motion.Relative.Y * sensitivity, -maxPitch, maxPitch);
			_head.Rotation = headRot;
		}
	}

	/// <summary>
	/// Starts a dash in the held move direction (forward if none). Returns false if the
	/// dash is locked, on cooldown or already under way.
	/// </summary>
	public bool TryDash()
	{
		if (!DashUnlocked || _dashCooldownTimer > 0f || IsDashing)
			return false;

		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 direction = Transform.Basis * new Vector3(input.X, 0f, input.Y);
		direction.Y = 0f;
		if (direction.LengthSquared() < 0.01f)
			direction = -Transform.Basis.Z;

		_dashVelocity = direction.Normalized() * DashSpeed;
		_dashTimer = DashDuration;
		_dashCooldownTimer = DashCooldown;
		_fovPunch = DashFovPunch;
		_dashSfx.PitchScale = (float)GD.RandRange(0.95, 1.08);
		_dashSfx.Play();
		EmitSignal(SignalName.Dashed, DashCooldown);
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		Vector3 velocity = Velocity;
		bool onFloor = IsOnFloor();

		// Jump forgiveness timers.
		_coyoteTimer = onFloor ? CoyoteTime : _coyoteTimer - dt;
		_jumpBufferTimer = Input.IsActionJustPressed("jump") ? JumpBufferTime : _jumpBufferTimer - dt;

		// Gravity: heavier on the way down for a punchier arc.
		if (!onFloor)
		{
			float multiplier = velocity.Y < 0f ? FallGravityMultiplier : 1f;
			velocity.Y -= _gravity * multiplier * dt;
		}

		if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
		{
			velocity.Y = Mathf.Sqrt(2f * _gravity * JumpHeight);
			_jumpBufferTimer = 0f;
			_coyoteTimer = 0f;
		}

		// Horizontal movement.
		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 wishDir = Transform.Basis * new Vector3(input.X, 0f, input.Y);
		wishDir.Y = 0f;
		wishDir = wishDir.LengthSquared() > 0f ? wishDir.Normalized() : Vector3.Zero;

		// Sprint only when pushing forward, so backpedalling stays slow.
		IsSprinting = Input.IsActionPressed("sprint") && input.Y < -0.1f && !IsAiming;
		float targetSpeed = IsSprinting ? SprintSpeed * SprintSpeedMultiplier : WalkSpeed * WalkSpeedMultiplier;
		if (IsAiming)
			targetSpeed *= AimMoveSpeedMultiplier;

		Vector3 horizontal = new(velocity.X, 0f, velocity.Z);
		bool hasInput = wishDir != Vector3.Zero;

		if (onFloor)
		{
			float rate = hasInput ? GroundAcceleration : GroundDeceleration;
			horizontal = horizontal.MoveToward(wishDir * targetSpeed, rate * dt);
		}
		else if (hasInput)
		{
			// In the air, steer but keep momentum when no keys are held.
			horizontal = horizontal.MoveToward(wishDir * targetSpeed, AirAcceleration * dt);
		}

		// Dash: a short burst that overrides normal movement, then hands back at sprint pace
		// so it can't be chained into a huge leap.
		_dashCooldownTimer -= dt;
		if (Input.IsActionJustPressed("dash"))
			TryDash();
		if (IsDashing)
		{
			_dashTimer -= dt;
			horizontal = _dashVelocity;
			if (!IsDashing)
				horizontal = horizontal.LimitLength(SprintSpeed * SprintSpeedMultiplier);
		}

		velocity.X = horizontal.X;
		velocity.Z = horizontal.Z;
		Velocity = velocity;
		MoveAndSlide();
	}

	public override void _Process(double delta)
	{
		// FOV kick only once actually moving faster than a walk, not just when Shift is held.
		float horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
		bool fastEnough = horizontalSpeed > WalkSpeed * WalkSpeedMultiplier + 0.5f;
		float targetFov = IsSprinting && fastEnough ? SprintFov : BaseFov;
		float speed = FovLerpSpeed;
		bool zooming = IsAiming || _fov < BaseFov - 0.5f;
		if (IsAiming)
			targetFov = AimFov;
		if (zooming)
			speed = AimFovLerpSpeed;
		float weight = 1f - Mathf.Exp(-speed * (float)delta);
		_fov = Mathf.Lerp(_fov, targetFov, weight);

		// The dash punch rides on top, so it never fights the sprint or aim easing.
		_fovPunch *= Mathf.Exp(-DashFovRecoverySpeed * (float)delta);
		_camera.Fov = _fov + _fovPunch;
	}
}
