using Godot;

namespace Deadset.Combat;

/// <summary>
/// Shared close-range attack: damage, reach and cooldown. The owning behaviour
/// decides when to swing (and how it looks); this only applies the hit.
/// </summary>
public partial class MeleeAttack : Node
{
	[Signal] public delegate void AttackLandedEventHandler(float damage);

	[Export] public float Damage { get; set; } = 5f;
	[Export] public float Range { get; set; } = 1.1f;
	[Export] public float Cooldown { get; set; } = 0.6f;

	public bool IsReady => _cooldownTimer <= 0f;

	private float _cooldownTimer;

	public override void _Ready()
	{
		// Only ticks while cooling down.
		SetPhysicsProcess(_cooldownTimer > 0f);
	}

	public override void _PhysicsProcess(double delta)
	{
		_cooldownTimer -= (float)delta;
		if (_cooldownTimer <= 0f)
			SetPhysicsProcess(false);
	}

	public void StartCooldown()
	{
		_cooldownTimer = Cooldown;
		SetPhysicsProcess(_cooldownTimer > 0f);
	}

	/// <summary>Deals damage to the target. Returns false if there was nothing alive to hit.</summary>
	public bool ApplyTo(HealthComponent target)
	{
		if (target == null || target.IsDead)
			return false;

		target.ApplyDamage(Damage);
		EmitSignal(SignalName.AttackLanded, Damage);
		return true;
	}
}
