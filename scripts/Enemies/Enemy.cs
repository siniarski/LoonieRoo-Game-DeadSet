using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Core;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// Shared enemy body: stats, health, target lookup, pathing, separation, gravity and death.
/// Each enemy type subclasses this with its own behaviour (chase, charge, ...).
/// Expects children: HealthComponent, Visual. Pathing comes from the shared NavGrid flow field,
/// so a NavigationAgent3D child is not needed (one left in the scene is removed on ready).
/// Behaviours must call BeginTick() first thing in _PhysicsProcess and EndTick() last.
/// </summary>
public abstract partial class Enemy : CharacterBody3D
{
	/// <summary>Every live enemy, for cheap separation checks without scene-tree queries.</summary>
	protected static readonly List<Enemy> All = new();

	/// <summary>How many enemies are alive right now.</summary>
	public static int AliveCount => All.Count;

	[Export] public EnemyStats Stats { get; set; }

	[ExportGroup("Death")]
	[Export] public Color DeathSparkColor { get; set; } = new(0.45f, 1f, 0.4f);
	[Export] public int DeathSparkAmount { get; set; } = 18;
	[Export] public EnemyCue DeathCue { get; set; } = EnemyCue.None;
	/// <summary>Height of the body centre, where death effects play.</summary>
	[Export] public float EffectHeight { get; set; } = 0.3f;
	/// <summary>Speed the body is knocked away from the player on death (0 = it just topples).</summary>
	[Export] public float CorpseKnockSpeed { get; set; } = 4.5f;
	[Export] public float CorpseUpSpeed { get; set; } = 3f;
	/// <summary>Seconds the body lies still before sinking, and how long the sink takes.</summary>
	[Export] public float CorpseHold { get; set; } = 0.5f;
	[Export] public float CorpseSink { get; set; } = 0.6f;

	/// <summary>Ignores gravity and keeps its height (birds). Still paths along the ground navmesh.</summary>
	[Export] public bool Flying { get; set; }

	/// <summary>Distance under which the enemy runs straight at the target instead of following the path.</summary>
	[Export] public float DirectChaseDistance { get; set; } = 3f;

	[ExportGroup("Performance")]
	/// <summary>
	/// On open, flat ground clear of cover, chase by sliding the body along instead of running the
	/// full collision move: there is nothing to bump into there but the target, which is handled
	/// as a simple circle. The collision move is by far the most expensive thing an enemy does.
	/// Turn off for maps with slopes or steps.
	/// </summary>
	[Export] public bool OpenGroundGlide { get; set; } = true;
	/// <summary>While gliding it still makes a full collision move every this many ticks, to stay glued to the floor.</summary>
	[Export] public int GlideResyncTicks { get; set; } = 30;
	/// <summary>
	/// If the target's feet are further than this above or below its own (the player jumped, or
	/// stands on cover), it uses the full collision move near them instead of the circle.
	/// </summary>
	[Export] public float GlideLevelTolerance { get; set; } = 0.35f;

	protected HealthComponent Health { get; private set; }
	protected Node3D Visual { get; private set; }
	protected Node3D Target { get; private set; }
	protected HealthComponent TargetHealth { get; private set; }
	protected float Gravity { get; private set; }

	protected bool HasLiveTarget => IsInstanceValid(Target) && TargetHealth != null && !TargetHealth.IsDead;

	/// <summary>This body's position at the start of the physics tick (set by BeginTick).</summary>
	protected Vector3 BodyPosition { get; private set; }
	/// <summary>The target's position at the start of the physics tick (set by BeginTick).</summary>
	protected Vector3 TargetPosition { get; private set; }
	/// <summary>On the floor as of the last move. Cached so behaviours don't ask the engine repeatedly.</summary>
	protected bool Grounded { get; private set; }

	private float _repathTimer;
	private NavGrid.Route _route = NavGrid.Route.Direct;
	private Vector3 _waypoint;
	private int _crowdIndex = -1;
	private ulong _crowdFrame = ulong.MaxValue;
	private int _glideTicks;
	private WeakSpot[] _weakSpots = System.Array.Empty<WeakSpot>();
	private float _bodyRadius;
	private static Node3D _radiusTarget;
	private static float _targetRadius = 0.4f;
	private float _yaw;

	public override void _EnterTree()
	{
		All.Add(this);
		AddToGroup("enemies");
	}
	public override void _ExitTree()
	{
		All.Remove(this);
		AttackSlots.Release(this);
	}

	public override void _Ready()
	{
		Health = GetNode<HealthComponent>("HealthComponent");
		Health.MaxHealth = Stats.MaxHealth;
		Health.ResetHealth();
		Health.Died += OnDied;

		Visual = GetNode<Node3D>("Visual");
		// Pathing is shared (NavGrid). A leftover agent would only cost a server update every tick.
		GetNodeOrNull("NavigationAgent3D")?.Free();
		Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		_repathTimer = (float)GD.RandRange(0.0, Stats.RepathInterval);  // stagger path requests
		_glideTicks = (int)(GD.Randi() % (uint)Mathf.Max(GlideResyncTicks, 1));  // stagger resyncs too
		_yaw = Visual.Rotation.Y;

		Target = GetTree().GetFirstNodeInGroup("player") as Node3D;
		TargetHealth = HealthComponent.FindOn(Target);

		_bodyRadius = FlatRadius(this);

		// Weak spots follow the animated head. Tick them from here rather than on their own.
		var spots = new List<WeakSpot>();
		foreach (Node child in GetChildren())
		{
			if (child is not WeakSpot spot)
				continue;
			spot.DriveExternally();
			spots.Add(spot);
		}
		if (spots.Count > 0)
			_weakSpots = spots.ToArray();
		if (Target != null && Target != _radiusTarget)
		{
			_radiusTarget = Target;
			_targetRadius = FlatRadius(Target);
		}
	}

	/// <summary>
	/// Refreshes BodyPosition and TargetPosition for this physics tick. The first enemy to tick
	/// in a frame also refreshes the shared crowd snapshot and points the flow field at the target.
	/// </summary>
	protected void BeginTick()
	{
		ulong frame = Engine.GetPhysicsFrames();
		if (frame != EnemyCrowd.Frame)
			RefreshCrowd(frame);

		if (_crowdFrame == frame)
			BodyPosition = EnemyCrowd.PositionOf(_crowdIndex);
		else
			BodyPosition = GlobalPosition;   // joined the tree after this tick's snapshot

		if (EnemyCrowd.Target == Target)
			TargetPosition = EnemyCrowd.TargetPosition;
		else if (IsInstanceValid(Target))
			TargetPosition = Target.GlobalPosition;
	}

	/// <summary>Call last thing each physics tick, once the body has moved and turned.</summary>
	protected void EndTick()
	{
		foreach (WeakSpot spot in _weakSpots)
			spot.Follow();
	}

	private static void RefreshCrowd(ulong frame)
	{
		Node3D target = null;
		foreach (Enemy enemy in All)
		{
			if (IsInstanceValid(enemy.Target))
			{
				target = enemy.Target;
				break;
			}
		}
		Vector3 targetPosition = target?.GlobalPosition ?? Vector3.Zero;

		EnemyCrowd.Begin(frame, All.Count, target, targetPosition);
		for (int i = 0; i < All.Count; i++)
		{
			Enemy enemy = All[i];
			enemy._crowdIndex = i;
			enemy._crowdFrame = frame;
			EnemyCrowd.Add(i, enemy.GlobalPosition);
		}

		if (target != null)
			NavGrid.UpdateTarget(targetPosition, frame);
	}

	/// <summary>
	/// Flat direction toward the target: straight at them when the way is clear or they are
	/// close, otherwise along the shared flow field around cover.
	/// </summary>
	protected Vector3 ChaseDirection(float dt)
	{
		Vector3 toTarget = TargetPosition - BodyPosition;
		toTarget.Y = 0f;

		_repathTimer -= dt;
		bool arrived = _route != NavGrid.Route.Direct && FlatDistanceSquared(_waypoint, BodyPosition) < 0.09f;
		if (_repathTimer <= 0f || arrived)
		{
			_repathTimer = Stats.RepathInterval;
			_route = NavGrid.FindRoute(BodyPosition, out _waypoint);
		}

		// Up close it runs straight in, unless cover is known to stand in the way.
		bool direct = _route == NavGrid.Route.Direct
			|| (_route == NavGrid.Route.Waypoint && toTarget.LengthSquared() <= DirectChaseDistance * DirectChaseDistance);
		if (!direct)
		{
			Vector3 toNext = _waypoint - BodyPosition;
			toNext.Y = 0f;
			if (toNext.LengthSquared() > 0.01f)
				return toNext.Normalized();
		}

		// Clear run, close by, or no route yet (navmesh still baking): run straight at them.
		return toTarget.LengthSquared() > 0.0001f ? toTarget.Normalized() : Vector3.Zero;
	}

	private static float FlatDistanceSquared(Vector3 a, Vector3 b)
	{
		float x = a.X - b.X, z = a.Z - b.Z;
		return x * x + z * z;
	}

	/// <summary>Push away from nearby enemies so swarms spread instead of stacking.</summary>
	protected Vector3 Separation()
	{
		if (_crowdFrame != EnemyCrowd.Frame)
			return Vector3.Zero;
		return EnemyCrowd.Separation(_crowdIndex, Stats.SeparationRadius) * Stats.SeparationStrength;
	}

	/// <summary>Accelerates horizontally toward desiredVelocity, applies gravity, then moves.</summary>
	protected void Steer(Vector3 desiredVelocity, float dt, bool allowSteering = true)
	{
		Vector3 velocity = Velocity;
		if (Flying)
			velocity.Y = 0f;
		else if (!Grounded)
			velocity.Y -= Gravity * dt;

		if (allowSteering)
		{
			Vector3 horizontal = new(velocity.X, 0f, velocity.Z);
			horizontal = horizontal.MoveToward(desiredVelocity, Stats.Acceleration * dt);
			velocity.X = horizontal.X;
			velocity.Z = horizontal.Z;
		}

		if (!CanGlide(velocity))
		{
			Velocity = velocity;
			Move();
			return;
		}

		Vector3 next = BodyPosition + new Vector3(velocity.X * dt, 0f, velocity.Z * dt);

		// The one thing to bump into out here is the target: keep out of its circle and slide round it.
		float reach = _bodyRadius + _targetRadius;
		float ox = next.X - TargetPosition.X, oz = next.Z - TargetPosition.Z;
		float squared = ox * ox + oz * oz;
		if (squared < reach * reach)
		{
			float distance = Mathf.Sqrt(squared);
			if (distance < 0.0001f)
			{
				ox = BodyPosition.X - TargetPosition.X;
				oz = BodyPosition.Z - TargetPosition.Z;
				distance = Mathf.Sqrt(ox * ox + oz * oz);
			}
			if (distance < 0.0001f)
			{
				ox = 1f;
				oz = 0f;
				distance = 1f;
			}
			ox /= distance;
			oz /= distance;
			next.X = TargetPosition.X + ox * reach;
			next.Z = TargetPosition.Z + oz * reach;
			float inward = velocity.X * ox + velocity.Z * oz;
			if (inward < 0f)
			{
				velocity.X -= ox * inward;
				velocity.Z -= oz * inward;
			}
		}

		Velocity = velocity;
		// Standing still on open ground costs nothing at all.
		if (FlatDistanceSquared(next, BodyPosition) > 1e-8f)
			GlobalPosition = next;
	}

	/// <summary>True when a plain slide along the ground is as good as a collision move this tick.</summary>
	private bool CanGlide(Vector3 velocity)
	{
		if (!OpenGroundGlide || !(Grounded || Flying) || Mathf.Abs(velocity.Y) > 0.01f)
			return false;
		if (_crowdFrame != EnemyCrowd.Frame)
			return false;   // BodyPosition is not from this tick's snapshot
		if (++_glideTicks >= GlideResyncTicks)
		{
			_glideTicks = 0;
			return false;
		}

		// Within touching distance of the target the circle only stands in for the real shapes
		// while both are standing on the same ground.
		float near = _bodyRadius + _targetRadius + 0.5f;
		if (FlatDistanceSquared(TargetPosition, BodyPosition) < near * near
			&& (Flying || Mathf.Abs(TargetPosition.Y - BodyPosition.Y) > GlideLevelTolerance))
			return false;

		return NavGrid.IsOpenGround(BodyPosition);
	}

	/// <summary>Horizontal radius of a body's main collision shape (ignoring weak spots).</summary>
	private static float FlatRadius(Node body)
	{
		float radius = 0f;
		foreach (Node child in body.GetChildren())
		{
			if (child is not CollisionShape3D { Shape: { } shape } node || child is WeakSpot)
				continue;
			bool upright = Mathf.Abs(node.Basis.Y.Y) > 0.7f;
			float r = shape switch
			{
				// A capsule or cylinder lying on its side reaches half its length.
				CapsuleShape3D capsule => upright ? capsule.Radius : Mathf.Max(capsule.Radius, capsule.Height * 0.5f),
				SphereShape3D sphere => sphere.Radius,
				CylinderShape3D cylinder => upright ? cylinder.Radius : Mathf.Max(cylinder.Radius, cylinder.Height * 0.5f),
				BoxShape3D box => Mathf.Max(box.Size.X, box.Size.Z) * 0.5f,
				_ => 0.5f,
			};
			Vector3 offset = node.Position;
			radius = Mathf.Max(radius, r + Mathf.Sqrt(offset.X * offset.X + offset.Z * offset.Z));
		}
		return radius > 0f ? radius : 0.5f;
	}

	/// <summary>MoveAndSlide, then refresh the cached floor state. Use instead of calling MoveAndSlide directly.</summary>
	protected void Move()
	{
		MoveAndSlide();
		Grounded = IsOnFloor();
	}

	/// <summary>Turns the Visual to face a flat direction.</summary>
	protected void FaceDirection(Vector3 direction, float dt, float turnSpeed = 14f)
	{
		if (direction.LengthSquared() < 0.01f)
			return;
		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		float yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-turnSpeed * dt));
		// Already facing that way: leave the node alone, a write would re-send the whole model.
		if (Mathf.Abs(Mathf.AngleDifference(_yaw, yaw)) < 0.002f)
			return;
		_yaw = yaw;
		Vector3 rotation = Visual.Rotation;
		rotation.Y = yaw;
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

		Vector3 away = IsInstanceValid(Target) ? GlobalPosition - Target.GlobalPosition : -GlobalBasis.Z;
		float groundY = Flying ? 0f : GlobalPosition.Y;
		Corpse.Spawn(parent, Visual, GlobalPosition, groundY, away, CorpseKnockSpeed, CorpseUpSpeed, CorpseHold, CorpseSink);

		QueueFree();
	}
}
