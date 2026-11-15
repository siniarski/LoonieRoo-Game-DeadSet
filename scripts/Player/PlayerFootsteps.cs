using Deadset.Effects;
using Godot;

namespace Deadset.Player;

/// <summary>
/// The player's own footsteps and landing thud. Reads the body's motion only, so it never
/// touches movement code.
/// </summary>
public partial class PlayerFootsteps : Node
{
	/// <summary>Metres per step while walking; sprinting uses SprintStride.</summary>
	[Export] public float WalkStride { get; set; } = 1.7f;
	[Export] public float SprintStride { get; set; } = 2.3f;
	[Export] public float StepVolumeDb { get; set; } = -14f;
	[Export] public float LandVolumeDb { get; set; } = -8f;
	/// <summary>Falls slower than this (m/s) on landing make no thud.</summary>
	[Export] public float MinLandSpeed { get; set; } = 4f;

	private PlayerController _body;
	private AudioStreamPlayer _step;
	private AudioStreamPlayer _land;
	private float _travelled;
	private bool _wasOnFloor = true;
	private float _fallSpeed;
	private bool _leftFoot;

	public override void _Ready()
	{
		_body = GetParent<PlayerController>();
		_step = new AudioStreamPlayer { Stream = PlaceholderSfx.PlayerStep, VolumeDb = StepVolumeDb };
		_land = new AudioStreamPlayer { Stream = PlaceholderSfx.PlayerLand, VolumeDb = LandVolumeDb };
		AddChild(_step);
		AddChild(_land);
	}

	public override void _PhysicsProcess(double delta)
	{
		bool onFloor = _body.IsOnFloor();
		Vector3 v = _body.Velocity;

		if (!onFloor)
			_fallSpeed = Mathf.Max(_fallSpeed, -v.Y);
		else if (!_wasOnFloor)
		{
			if (_fallSpeed >= MinLandSpeed)
			{
				_land.PitchScale = (float)GD.RandRange(0.9, 1.05);
				_land.Play();
			}
			_fallSpeed = 0f;
			_travelled = 0f;
		}
		_wasOnFloor = onFloor;

		if (!onFloor)
			return;
		_travelled += new Vector2(v.X, v.Z).Length() * (float)delta;
		float stride = _body.IsSprinting ? SprintStride : WalkStride;
		if (_travelled < stride)
			return;
		_travelled = 0f;
		_leftFoot = !_leftFoot;
		_step.PitchScale = (_leftFoot ? 0.95f : 1.05f) + (float)GD.RandRange(-0.04, 0.04);
		_step.Play();
	}
}
