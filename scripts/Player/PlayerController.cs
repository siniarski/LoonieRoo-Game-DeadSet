using Godot;

namespace Deadset.Player;

/// <summary>
/// First-person movement and mouse look for The Tradie.
/// Owns movement only: weapons, health and upgrades live in their own components.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 5.5f;
	[Export] public float SprintSpeed { get; set; } = 8.5f;
	/// <summary>How fast you reach target speed on the ground (m/s²).</summary>
	[Export] public float GroundAcceleration { get; set; } = 60f;
	/// <summary>How fast you stop on the ground with no input (m/s²).</summary>
	[Export] public float GroundDeceleration { get; set; } = 45f;
	/// <summary>Steering in the air (m/s²). Lower = more committed jumps.</summary>
	[Export] public float AirAcceleration { get; set; } = 14f;

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

	[ExportGroup("Camera")]
	[Export(PropertyHint.Range, "50,120")] public float BaseFov { get; set; } = 80f;
	[Export(PropertyHint.Range, "50,130")] public float SprintFov { get; set; } = 88f;
	/// <summary>How quickly the FOV eases toward its target. Higher = snappier.</summary>
	[Export] public float FovLerpSpeed { get; set; } = 8f;

	/// <summary>True while sprint is held and the player is moving forward.</summary>
	public bool IsSprinting { get; private set; }

	private Node3D _head;
	private Camera3D _camera;
	private float _gravity;
	private float _coyoteTimer;
	private float _jumpBufferTimer;

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head");
		_camera = GetNode<Camera3D>("Head/Camera3D");
		_camera.Fov = BaseFov;
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		Input.MouseMode = Input.MouseModeEnum.Captured;
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
			if (@event is InputEventMouseButton { Pressed: true })
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (@event is InputEventMouseMotion motion)
		{
			RotateY(-motion.Relative.X * MouseSensitivity);
			float maxPitch = Mathf.DegToRad(MaxPitchDegrees);
			Vector3 headRot = _head.Rotation;
			headRot.X = Mathf.Clamp(headRot.X - motion.Relative.Y * MouseSensitivity, -maxPitch, maxPitch);
			_head.Rotation = headRot;
		}
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
		IsSprinting = Input.IsActionPressed("sprint") && input.Y < -0.1f;
		float targetSpeed = IsSprinting ? SprintSpeed : WalkSpeed;

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

		velocity.X = horizontal.X;
		velocity.Z = horizontal.Z;
		Velocity = velocity;
		MoveAndSlide();
	}

	public override void _Process(double delta)
	{
		// FOV kick only once actually moving faster than a walk, not just when Shift is held.
		float horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
		bool fastEnough = horizontalSpeed > WalkSpeed + 0.5f;
		float targetFov = IsSprinting && fastEnough ? SprintFov : BaseFov;
		float weight = 1f - Mathf.Exp(-FovLerpSpeed * (float)delta);
		_camera.Fov = Mathf.Lerp(_camera.Fov, targetFov, weight);
	}
}
