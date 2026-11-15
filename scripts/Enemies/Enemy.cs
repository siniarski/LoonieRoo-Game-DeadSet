using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Core;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// Shared enemy body: stats, health, target lookup, pathing, separation, gravity and death.
/// Each enemy type subclasses this with its own behaviour (chase, charge, ...).
/// Expects children: HealthComponent, Visual, NavigationAgent3D.
/// </summary>
public abstract partial class Enemy : CharacterBody3D
{
	/// <summary>Every live enemy, for cheap separation checks without scene-tree queries.</summary>
	protected static readonly List<Enemy> All = new();

	[Export] public EnemyStats Stats { get; set; }

	[ExportGroup("Death")]
	[Export] public Color DeathSparkColor { get; set; } = new(0.45f, 1f, 0.4f);
	[Export] public int DeathSparkAmount { get; set; } = 18;
	[Export] public EnemyCue DeathCue { get; set; } = EnemyCue.None;
	/// <summary>Height of the body centre, where death effects play.</summary>
	[Export] public float EffectHeight { get; set; } = 0.3f;

	/// <summary>Ignores gravity and keeps its height (birds). Still paths along the ground navmesh.</summary>
	[Export] public bool Flying { get; set; }

	/// <summary>Distance under which the enemy runs straight at the target instead of following the path.</summary>
	[Export] public float DirectChaseDistance { get; set; } = 3f;

	protected HealthComponent Health { get; private set; }
	protected Node3D Visual { get; private set; }
	protected Node3D Target { get; private set; }
	protected HealthComponent TargetHealth { get; private set; }
	protected float Gravity { get; private set; }

	protected bool HasLiveTarget => IsInstanceValid(Target) && TargetHealth != null && !TargetHealth.IsDead;

	private NavigationAgent3D _agent;
	private float _repathTimer;

	public override void _EnterTree()
	{
		All.Add(this);
		AddToGroup("enemies");
	}
	public override void _ExitTree() => All.Remove(this);

	public override void _Ready()
	{
		Health = GetNode<HealthComponent>("HealthComponent");
		Health.MaxHealth = Stats.MaxHealth;
		Health.ResetHealth();
		Health.Died += OnDied;

		Visual = GetNode<Node3D>("Visual");
		_agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		_repathTimer = (float)GD.RandRange(0.0, Stats.RepathInterval);  // stagger path requests

		Target = GetTree().GetFirstNodeInGroup("player") as Node3D;
		TargetHealth = HealthComponent.FindOn(Target);
	}

	/// <summary>Flat direction toward the target: follows the navmesh at range, straight line up close.</summary>
	protected Vector3 ChaseDirection(float dt)
	{
		Vector3 toTarget = Target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0f;

		if (toTarget.Length() > DirectChaseDistance)
		{
			_repathTimer -= dt;
			if (_repathTimer <= 0f)
			{
				_repathTimer = Stats.RepathInterval;
				_agent.TargetPosition = Target.GlobalPosition;
			}

			Vector3 toNext = _agent.GetNextPathPosition() - GlobalPosition;
			toNext.Y = 0f;
			if (toNext.LengthSquared() > 0.01f)
				return toNext.Normalized();
		}

		// Close by, or no path yet (navmesh still baking): run straight at them.
		return toTarget.LengthSquared() > 0.0001f ? toTarget.Normalized() : Vector3.Zero;
	}

	/// <summary>Push away from nearby enemies so swarms spread instead of stacking.</summary>
	protected Vector3 Separation()
	{
		Vector3 push = Vector3.Zero;
		float radius = Stats.SeparationRadius;
		Vector3 me = GlobalPosition;

		foreach (Enemy other in All)
		{
			if (other == this)
				continue;
			Vector3 away = me - other.GlobalPosition;
			away.Y = 0f;
			float distance = away.Length();
			if (distance >= radius)
				continue;
			if (distance < 0.001f)
				away = new Vector3(GD.Randf() - 0.5f, 0f, GD.Randf() - 0.5f);
			push += away.Normalized() * (1f - distance / radius);
		}

		return push * Stats.SeparationStrength;
	}

	/// <summary>Accelerates horizontally toward desiredVelocity, applies gravity, then moves.</summary>
	protected void Steer(Vector3 desiredVelocity, float dt, bool allowSteering = true)
	{
		Vector3 velocity = Velocity;
		if (Flying)
			velocity.Y = 0f;
		else if (!IsOnFloor())
			velocity.Y -= Gravity * dt;

		if (allowSteering)
		{
			Vector3 horizontal = new(velocity.X, 0f, velocity.Z);
			horizontal = horizontal.MoveToward(desiredVelocity, Stats.Acceleration * dt);
			velocity.X = horizontal.X;
			velocity.Z = horizontal.Z;
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	/// <summary>Turns the Visual to face a flat direction.</summary>
	protected void FaceDirection(Vector3 direction, float dt, float turnSpeed = 14f)
	{
		if (direction.LengthSquared() < 0.01f)
			return;
		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		Vector3 rotation = Visual.Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetYaw, 1f - Mathf.Exp(-turnSpeed * dt));
		Visual.Rotation = rotation;
	}

	protected virtual AudioStream DeathSound => PlaceholderSfx.ForCue(DeathCue);

	protected virtual void OnDied()
	{
		GameEvents.Instance?.EmitSignal(GameEvents.SignalName.EnemyKilled, this);

		Node parent = GetParent();
		Vector3 center = GlobalPosition + Vector3.Up * EffectHeight;
		ImpactEffects.SpawnSparks(parent, center, Vector3.Up, DeathSparkColor, DeathSparkAmount);
		if (DeathSound != null)
			PlaceholderSfx.PlayAt(parent, DeathSound, center, -6f, 0.15f);

		QueueFree();
	}
}
