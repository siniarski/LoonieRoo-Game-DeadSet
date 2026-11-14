using Deadset.Combat;
using Godot;

namespace Deadset.Debug;

/// <summary>
/// Test target for the weapon milestone. Falls over when its health runs out,
/// then stands back up with full health.
/// </summary>
public partial class TargetDummy : StaticBody3D
{
	[Export] public float FallTime { get; set; } = 0.45f;
	[Export] public float RespawnDelay { get; set; } = 2f;
	[Export] public float StandUpTime { get; set; } = 0.35f;

	private HealthComponent _health;
	private float _uprightPitch;

	public override void _Ready()
	{
		_health = GetNode<HealthComponent>("HealthComponent");
		_health.Died += OnDied;
		_uprightPitch = Rotation.X;
	}

	private void OnDied()
	{
		Tween tween = CreateTween();
		tween.TweenProperty(this, "rotation:x", _uprightPitch - Mathf.Pi / 2f, FallTime)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.TweenInterval(RespawnDelay);
		tween.TweenCallback(Callable.From(_health.ResetHealth));
		tween.TweenProperty(this, "rotation:x", _uprightPitch, StandUpTime)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}
}
