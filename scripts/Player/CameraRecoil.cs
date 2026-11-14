using Godot;

namespace Deadset.Player;

/// <summary>
/// Camera-side recoil. Kicks snap in fast, then ease back to centre, so aim recovers on its own.
/// Applied as a rotation offset on the camera, separate from mouse look on the Head.
/// </summary>
public partial class CameraRecoil : Camera3D
{
	/// <summary>How fast the camera reaches the kick. Higher = punchier.</summary>
	[Export] public float SnapSpeed { get; set; } = 40f;
	/// <summary>How fast the kick returns to centre.</summary>
	[Export] public float RecoverySpeed { get; set; } = 9f;
	/// <summary>Cap on accumulated upward kick, in degrees.</summary>
	[Export] public float MaxPitchDegrees { get; set; } = 8f;

	private Vector2 _target;  // x = pitch, y = yaw, in degrees
	private Vector2 _current;

	public void AddKick(float pitchDegrees, float yawDegrees)
	{
		_target.X = Mathf.Min(_target.X + pitchDegrees, MaxPitchDegrees);
		_target.Y += yawDegrees;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_target = _target.Lerp(Vector2.Zero, 1f - Mathf.Exp(-RecoverySpeed * dt));
		_current = _current.Lerp(_target, 1f - Mathf.Exp(-SnapSpeed * dt));
		RotationDegrees = new Vector3(_current.X, _current.Y, 0f);
	}
}
