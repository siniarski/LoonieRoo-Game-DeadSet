using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Weapons;

/// <summary>
/// A hero's always-on melee (the Tradie's claw hammer). It isn't a slot weapon: the wielder
/// swings it with the melee key, or with the trigger once the gun in hand is completely dry, so
/// a player who's out of ammo can always finish a wave. Each swing hits the nearest enemy in a
/// narrow cone. Aim at the head for a head hit, so the guns' skill target pays off here too.
/// Each hero gets its own melee scene, with its own numbers and perk (see HeroDefinition).
/// </summary>
public partial class QuickMeleeWeapon : Weapon
{
	/// <summary>Full width of the swing, in degrees.</summary>
	[Export] public float ConeDegrees { get; set; } = 60f;
	/// <summary>Enemies are this wide: they're hit when their edge is in reach.</summary>
	[Export] public float EnemyRadius { get; set; } = 0.45f;
	/// <summary>A swing counts as a head hit when the crosshair is within this many degrees of the head.</summary>
	[Export] public float HeadAimDegrees { get; set; } = 14f;
	/// <summary>Seconds the swing takes on screen; the gun in hand can't fire meanwhile.</summary>
	[Export] public float SwingTime { get; set; } = 0.32f;

	[ExportGroup("Perk")]
	/// <summary>
	/// The Tradie's perk: a hammer kill pulls nails back out. Each kill puts this many rounds
	/// back into the reserve of the carried weapon using RefundStats (0 = no perk).
	/// </summary>
	[Export] public int RefundOnKill { get; set; } = 3;
	[Export] public WeaponStats RefundStats { get; set; }

	[ExportGroup("Feel")]
	[Export] public float SwingVolumeDb { get; set; } = -8f;
	[Export] public Color HitSparkColor { get; set; } = new(1f, 0.85f, 0.5f);

	public bool IsSwinging => _swing < 1f;

	private Node3D _model;
	private Vector3 _restRotation;
	private float _swing = 1f;
	private AudioStreamPlayer _swingSfx;
	private AudioStreamPlayer _hitSfx;

	public override void _Ready()
	{
		base._Ready();
		_model = GetNode<Node3D>("Model");
		_restRotation = _model.RotationDegrees;
		_model.Visible = false;
		_swingSfx = new AudioStreamPlayer { Stream = PlaceholderSfx.DashWhoosh, VolumeDb = SwingVolumeDb, MaxPolyphony = 2 };
		_hitSfx = new AudioStreamPlayer { VolumeDb = -3f, MaxPolyphony = 2 };
		AddChild(_swingSfx);
		AddChild(_hitSfx);
	}

	protected override void FireShot()
	{
		_swing = 0f;
		_model.Visible = true;
		_swingSfx.PitchScale = (float)GD.RandRange(1.5, 1.8);
		_swingSfx.Play();

		Transform3D aim = AimSource.GlobalTransform;
		Vector3 origin = aim.Origin;
		Vector3 look = -aim.Basis.Z;
		Vector3 forward = new Vector3(look.X, 0f, look.Z).Normalized();
		float reach = Stats.MaxRange;
		float halfCone = Mathf.DegToRad(ConeDegrees) * 0.5f;

		Node3D best = null;
		HealthComponent bestHealth = null;
		float bestDistance = float.MaxValue;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemy || enemy.IsQueuedForDeletion())
				continue;
			Vector3 offset = enemy.GlobalPosition - origin;
			if (offset.Y > 1.0f || offset.Y < -2.2f)
				continue;
			offset.Y = 0f;
			float distance = offset.Length();
			if (distance - EnemyRadius > reach || distance >= bestDistance)
				continue;
			if (distance > EnemyRadius * 1.5f && forward.AngleTo(offset) > halfCone)
				continue;
			HealthComponent health = HealthComponent.FindOn(enemy);
			if (health == null || health.IsDead)
				continue;
			best = enemy;
			bestHealth = health;
			bestDistance = distance;
		}
		if (best == null)
			return;

		float damage = Stats.Damage * DamageMultiplier;
		var head = best.GetNodeOrNull<WeakSpot>("HeadWeakSpot");
		bool headHit = head != null && look.AngleTo(head.GlobalPosition - origin) < Mathf.DegToRad(HeadAimDegrees);
		if (headHit)
			damage *= head.EffectiveMultiplier;
		Vector3 at = headHit ? head.GlobalPosition : best.GlobalPosition + Vector3.Up * 0.8f;
		bool killed = bestHealth.ApplyDamage(damage);
		ReportHit(killed, headHit);
		ImpactEffects.SpawnSparks(GetTree().CurrentScene, at, (origin - at).Normalized(), HitSparkColor, headHit ? 20 : 10);
		_hitSfx.Stream = PlaceholderSfx.Thump;
		_hitSfx.PitchScale = headHit ? 1.5f : 1.1f;
		_hitSfx.Play();
	}

	public override void _Process(double delta)
	{
		if (_swing >= 1f)
			return;
		_swing = Mathf.Min(_swing + (float)delta / Mathf.Max(SwingTime, 0.05f), 1f);
		// Raised over the shoulder, snapped down across the view, then put away.
		float t = _swing;
		float arc = t < 0.25f
			? Mathf.Lerp(55f, 70f, t / 0.25f)
			: Mathf.Lerp(70f, -75f, Mathf.Ease((t - 0.25f) / 0.75f, 0.35f));
		_model.RotationDegrees = _restRotation + new Vector3(arc, -10f * t, 25f - 35f * t);
		if (_swing >= 1f)
			_model.Visible = false;
	}
}
