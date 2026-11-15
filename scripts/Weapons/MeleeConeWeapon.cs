using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Weapons;

/// <summary>
/// A close-range weapon that hits every enemy in a cone in front of the player on each
/// swing (the Brushcutter). Reach is Stats.MaxRange. Its ammo is trimmer line: each enemy
/// hit uses LinePerHit, a swing that hits nothing is free, and a reload bump-feeds more line
/// off the spool. Owns its own look and sound: a spinning head that revs while the trigger is
/// held and sways as it cuts.
/// </summary>
public partial class MeleeConeWeapon : Weapon
{
	/// <summary>Full width of the cutting arc, in degrees (before reach mods).</summary>
	[Export] public float ConeDegrees { get; set; } = 80f;
	/// <summary>Most enemies one swing hits (before mods), nearest first.</summary>
	[Export] public int MaxTargets { get; set; } = 3;
	/// <summary>Enemies are this wide: they're hit when their edge is in reach, not their centre.</summary>
	[Export] public float EnemyRadius { get; set; } = 0.45f;
	[Export] public Color CutSparkColor { get; set; } = new(0.55f, 1f, 0.45f);
	/// <summary>Line used per enemy hit.</summary>
	[Export] public int LinePerHit { get; set; } = 1;

	protected override bool AmmoPerHit => true;

	[ExportGroup("Feel")]
	/// <summary>Blade spin at idle and while cutting, in turns per second.</summary>
	[Export] public float IdleSpin { get; set; } = 3f;
	[Export] public float CutSpin { get; set; } = 22f;
	/// <summary>How far the blade head sways side to side while cutting, in degrees.</summary>
	[Export] public float SwayDegrees { get; set; } = 16f;
	[Export] public float SwingVolumeDb { get; set; } = -9f;
	[Export] public float HitVolumeDb { get; set; } = -9f;

	private Node3D _model;
	private Node3D _blade;
	private Vector3 _modelRestRotation;
	private float _spin;
	private float _spinSpeed;
	private float _cutting;
	private float _swayPhase;
	private float _kick;
	private float _bump;
	private AudioStreamPlayer _swingSfx;
	private AudioStreamPlayer _hitSfx;
	private readonly List<(float distance, Node3D enemy, HealthComponent health)> _targets = new();

	public override void _Ready()
	{
		base._Ready();
		_model = GetNode<Node3D>("Model");
		_blade = GetNode<Node3D>("Model/Head/Blade");
		_modelRestRotation = _model.RotationDegrees;
		_swingSfx = new AudioStreamPlayer { Stream = PlaceholderSfx.Brushcutter, VolumeDb = SwingVolumeDb, MaxPolyphony = 2 };
		_hitSfx = new AudioStreamPlayer { VolumeDb = HitVolumeDb, MaxPolyphony = 3 };
		var handling = new AudioStreamPlayer { VolumeDb = HitVolumeDb + 2f };
		AddChild(_swingSfx);
		AddChild(_hitSfx);
		AddChild(handling);
		HitLanded += OnHitLanded;
		// Reload = tap the head on the ground to bump-feed more line.
		ReloadStarted += _ => { handling.Stream = PlaceholderSfx.ReloadStart; handling.Play(); };
		ReloadFinished += () => { handling.Stream = PlaceholderSfx.ReloadEnd; handling.Play(); };
	}

	public override void Holster()
	{
		base.Holster();
		_cutting = 0f;
	}

	protected override void FireShot()
	{
		_cutting = 1f;
		_kick = 1f;
		_swingSfx.PitchScale = (float)GD.RandRange(0.95, 1.08) * Mathf.Sqrt(FireRateMultiplier);
		_swingSfx.Play();

		Transform3D aim = AimSource.GlobalTransform;
		Vector3 origin = aim.Origin;
		Vector3 forward = -aim.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		float reach = Stats.MaxRange * ReachMultiplier;
		float halfCone = Mathf.DegToRad(ConeDegrees * ReachMultiplier) * 0.5f;
		int maxTargets = MaxTargets + ExtraTargets;

		_targets.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemy || enemy.IsQueuedForDeletion())
				continue;
			Vector3 offset = enemy.GlobalPosition - origin;
			// Ignore anything well above or below the player (a bird overhead).
			if (offset.Y > 1.2f || offset.Y < -2.2f)
				continue;
			offset.Y = 0f;
			float distance = offset.Length();
			if (distance - EnemyRadius > reach)
				continue;
			// Right up against you counts whatever the angle, so a pecker at your shoulder gets cut.
			if (distance > EnemyRadius * 1.5f && forward.AngleTo(offset) > halfCone)
				continue;
			HealthComponent health = HealthComponent.FindOn(enemy);
			if (health == null || health.IsDead)
				continue;
			_targets.Add((distance, enemy, health));
		}
		_targets.Sort((a, b) => a.distance.CompareTo(b.distance));

		float damage = Stats.Damage * DamageMultiplier;
		Node effects = GetTree().CurrentScene;
		// Out of line partway through a swing: it only reaches as many as there's line for.
		maxTargets = Mathf.Min(maxTargets, Mathf.Max(1, AmmoInMagazine / Mathf.Max(LinePerHit, 1)));
		for (int i = 0; i < _targets.Count && i < maxTargets; i++)
		{
			(_, Node3D enemy, HealthComponent health) = _targets[i];
			Vector3 at = enemy.GlobalPosition + Vector3.Up * 0.9f;
			bool killed = health.ApplyDamage(damage);
			ReportHit(killed);
			ImpactEffects.SpawnSparks(effects, at, (origin - at).Normalized(), CutSparkColor, 9);
			UseAmmo(LinePerHit);
		}
	}

	private void OnHitLanded(bool killed, bool weakSpot)
	{
		_hitSfx.Stream = killed ? PlaceholderSfx.KillTick : PlaceholderSfx.HitTick;
		_hitSfx.Play();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_cutting = Mathf.MoveToward(_cutting, 0f, dt * 2.5f);
		_kick *= Mathf.Exp(-14f * dt);
		float targetSpin = Mathf.Lerp(IdleSpin, CutSpin, Mathf.Min(_cutting * 2f, 1f));
		_spinSpeed = Mathf.Lerp(_spinSpeed, targetSpin, 1f - Mathf.Exp(-8f * dt));
		_spin = Mathf.Wrap(_spin + _spinSpeed * Mathf.Tau * dt, 0f, Mathf.Tau);
		_blade.Rotation = new Vector3(0f, _spin, 0f);

		// Sway the head across the cut while cutting, with a little buzz.
		_swayPhase += dt * 7f * FireRateMultiplier;
		float sway = Mathf.Sin(_swayPhase) * SwayDegrees * Mathf.Min(_cutting * 2f, 1f);
		float buzz = (float)GD.RandRange(-1.0, 1.0) * 0.6f * Mathf.Min(_cutting * 2f, 1f);
		// Bump feed: the head dips to tap the ground while reloading.
		_bump = Mathf.Lerp(_bump, IsReloading ? 1f : 0f, 1f - Mathf.Exp(-12f * dt));
		float tap = IsReloading ? Mathf.Abs(Mathf.Sin(ReloadProgress * Mathf.Pi * 2f)) : 0f;
		_model.RotationDegrees = _modelRestRotation
			+ new Vector3(buzz + Stats.ModelKickDegrees * _kick - 14f * _bump - 10f * tap, sway, buzz);
	}
}
