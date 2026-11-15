using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Player;

/// <summary>
/// "You got hurt" feel on the player side: a camera jolt and a thud.
/// The red screen flash lives in the HUD, which listens to the same health signal.
/// </summary>
public partial class PlayerDamageFeedback : Node
{
	[Export] public NodePath HealthPath { get; set; } = "../HealthComponent";
	[Export] public NodePath CameraPath { get; set; } = "../Head/Camera3D";
	/// <summary>Maximum random camera jolt per hit, in degrees.</summary>
	[Export] public float JoltDegrees { get; set; } = 2.5f;
	/// <summary>Hits at or above this damage get the full heavy jolt (a wombat charge).</summary>
	[Export] public float HeavyHitDamage { get; set; } = 20f;
	/// <summary>Jolt multiplier for a full heavy hit. Small nips stay at 1x.</summary>
	[Export] public float HeavyJoltMultiplier { get; set; } = 3.5f;
	[Export] public float HurtVolumeDb { get; set; } = -4f;
	/// <summary>Minimum seconds between hurt sounds, so a swarm doesn't machine-gun it.</summary>
	[Export] public float MinSoundInterval { get; set; } = 0.12f;

	private CameraRecoil _camera;
	private AudioStreamPlayer _hurtSfx;
	private double _lastSoundTime = -1.0;

	public override void _Ready()
	{
		_camera = GetNode<CameraRecoil>(CameraPath);
		_hurtSfx = new AudioStreamPlayer { Stream = PlaceholderSfx.PlayerHurt, VolumeDb = HurtVolumeDb };
		AddChild(_hurtSfx);
		GetNode<HealthComponent>(HealthPath).Damaged += OnDamaged;
	}

	private void OnDamaged(float amount, float remaining)
	{
		// Small hits jolt randomly; heavy hits also snap the view upward.
		float heavy = Mathf.Clamp((amount - 5f) / Mathf.Max(HeavyHitDamage - 5f, 1f), 0f, 1f);
		float jolt = JoltDegrees * Mathf.Lerp(1f, HeavyJoltMultiplier, heavy);
		float pitch = (float)GD.RandRange(-jolt, jolt) + jolt * heavy;
		float yaw = (float)GD.RandRange(-jolt, jolt);
		_camera.AddKick(pitch, yaw);

		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastSoundTime >= MinSoundInterval)
		{
			_lastSoundTime = now;
			_hurtSfx.PitchScale = (float)GD.RandRange(0.9, 1.1);
			_hurtSfx.Play();
		}
	}
}
