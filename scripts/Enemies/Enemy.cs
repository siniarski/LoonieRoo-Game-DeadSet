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
	/// <summary>Set by whoever spawns it, before it enters the tree (late waves are tougher).</summary>
	public float HealthMultiplier { get; set; } = 1f;

	/// <summary>
	/// Makes this enemy tougher and wider. Call before adding it to the tree, so the hit
	/// reaction picks up the new size as its rest pose.
	/// </summary>
	public void MakeChunky(float healthMultiplier, float width)
	{
		HealthMultiplier = healthMultiplier;
		if (width > 1.001f && GetNodeOrNull<Node3D>("Visual") is { } visual)
			visual.Scale = new Vector3(width, 1f + (width - 1f) * 0.15f, width);
	}

	/// <summary>
	/// Paths round to a spot behind the player (away from where the rest are coming from)
	/// before closing in. Set by whoever spawns it.
	/// </summary>
	public bool IsFlanker
	{
		get => _isFlanker;
		set { _isFlanker = value; _flanking = value; }
	}
	/// <summary>The flank spot sits this far behind the player.</summary>
	public static float FlankOffset { get; set; } = 7f;
	/// <summary>Only enemies within this distance count when working out which way the pressure comes from.</summary>
	public static float FlankScanRadius { get; set; } = 25f;
	/// <summary>A flanker stops flanking and just attacks once this close to the player, or after FlankTimeout seconds.</summary>
	public static float FlankReleaseDistance { get; set; } = 3f;
	/// <summary>Flankers route round the player at least this far out, instead of through them.</summary>
	public static float FlankAvoidRadius { get; set; } = 5f;
	public static float FlankTimeout { get; set; } = 15f;

	private bool _isFlanker;
	private bool _flanking;
	private float _flankTime;

	[ExportGroup("Death")]
	[Export] public Color DeathSparkColor { get; set; } = new(0.45f, 1f, 0.4f);
	[Export] public int DeathSparkAmount { get; set; } = 18;
	[Export] public EnemyCue DeathCue { get; set; } = EnemyCue.None;
	/// <summary>Sparks thrown when a head-shot kill pops the head off.</summary>
	[Export] public int HeadPopSparkAmount { get; set; } = 40;
	[Export] public Color HeadPopSparkColor { get; set; } = new(1f, 0.95f, 0.55f);
	[Export] public float HeadPopVolumeDb { get; set; } = -2f;
	/// <summary>Height of the body centre, where death effects play.</summary>
	[Export] public float EffectHeight { get; set; } = 0.3f;
	/// <summary>Speed the body is knocked away from the player on death (0 = it just topples).</summary>
	[Export] public float CorpseKnockSpeed { get; set; } = 4.5f;
	[Export] public float CorpseUpSpeed { get; set; } = 3f;
	/// <summary>Seconds the body lies still before sinking, and how long the sink takes.</summary>
	[Export] public float CorpseHold { get; set; } = 0.5f;
	[Export] public float CorpseSink { get; set; } = 0.6f;

	[ExportGroup("Footsteps")]
	/// <summary>Metres travelled per footstep sound (0 = silent).</summary>
	[Export] public float StrideLength { get; set; } = 1.3f;
	/// <summary>Heavy enemies thud; light ones scratch.</summary>
	[Export] public bool HeavyFootsteps { get; set; }
	[Export] public float FootstepVolumeDb { get; set; } = -8f;
	/// <summary>Footsteps further than this from the player are not played.</summary>
	[Export] public float FootstepRange { get; set; } = 16f;

	[ExportGroup("")]
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
		RangedSlots.Release(this);
	}

	public override void _Ready()
	{
		Health = GetNode<HealthComponent>("HealthComponent");
		Health.MaxHealth = Stats.MaxHealth * HealthMultiplier;
		Health.ResetHealth();
		Health.Died += OnDied;
		if (Stats.AlwaysFlanks)
			IsFlanker = true;
		ReadyVariant();

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
			spot.Toughness = HealthMultiplier;
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

		TickVariant((float)GetPhysicsProcessDeltaTime());

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
		TickFootsteps();
	}

	private float _strideTravelled;

	/// <summary>
	/// Plays a footstep every StrideLength metres of ground travel, if near the player, so a
	/// flock coming up behind you can be heard. The per-sound voice cap keeps a horde in check.
	/// </summary>
	private void TickFootsteps()
	{
		if (Flying || StrideLength <= 0f)
			return;
		float dt = (float)GetPhysicsProcessDeltaTime();
		Vector3 v = Velocity;
		_strideTravelled += new Vector2(v.X, v.Z).Length() * dt;
		if (_strideTravelled < StrideLength)
			return;
		_strideTravelled -= StrideLength * Mathf.Floor(_strideTravelled / StrideLength);
		if (BodyPosition.DistanceSquaredTo(TargetPosition) > FootstepRange * FootstepRange)
			return;
		PlaceholderSfx.PlayAt(GetParent(), HeavyFootsteps ? PlaceholderSfx.FootHeavy : PlaceholderSfx.FootLight,
			BodyPosition, FootstepVolumeDb, 0.15f);
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
		{
			NavGrid.UpdateTarget(targetPosition, frame);
			UpdateFlankGoal(targetPosition, frame);
		}
	}

	/// <summary>
	/// Points the flank field at a spot behind the player, opposite the side most enemies are
	/// coming from. Only when the pressure is lopsided; a player already surrounded needs no flank.
	/// </summary>
	private static void UpdateFlankGoal(Vector3 targetPosition, ulong frame)
	{
		if (frame % 15 != 0)
			return;
		// Which way is the pressure coming from? Enemies already flanking don't count, unless
		// they're all there is (a pack still on its way in).
		Vector3 pressure = Vector3.Zero, all = Vector3.Zero;
		int count = 0, allCount = 0;
		bool anyFlanking = false;
		float scan = FlankScanRadius * FlankScanRadius;
		foreach (Enemy enemy in All)
		{
			Vector3 offset = enemy.GlobalPosition - targetPosition;
			offset.Y = 0f;
			float d2 = offset.LengthSquared();
			if (d2 < 0.01f)
				continue;
			Vector3 unit = offset / Mathf.Sqrt(d2);
			all += unit;
			allCount++;
			if (enemy._flanking)
			{
				anyFlanking = true;
				continue;
			}
			if (d2 > scan)
				continue;
			pressure += unit;
			count++;
		}
		if (!anyFlanking)
			return;
		if (count < 3)
		{
			pressure = all;
			count = allCount;
		}
		if (count == 0 || pressure.Length() / count < 0.35f)
		{
			NavGrid.ClearFlank();
			return;
		}
		Vector3 behind = targetPosition - pressure.Normalized() * FlankOffset;
		NavGrid.UpdateFlank(behind, targetPosition, FlankAvoidRadius, frame, 30);
	}

	/// <summary>
	/// Flat direction toward the target: straight at them when the way is clear or they are
	/// close, otherwise along the shared flow field around cover.
	/// </summary>
	protected Vector3 ChaseDirection(float dt)
	{
		Vector3 toTarget = TargetPosition - BodyPosition;
		toTarget.Y = 0f;

		if (_flanking)
		{
			Vector3 flank = FlankDirection(dt, toTarget);
			if (flank != Vector3.Zero)
				return flank;
		}

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

	/// <summary>
	/// Where a flanker heads while it is still going round. Zero once it is done flanking
	/// (close to the player, at the flank spot, timed out, or there is no flank to make).
	/// </summary>
	private Vector3 FlankDirection(float dt, Vector3 toTarget)
	{
		_flankTime += dt;
		if (!NavGrid.FlankReady)
			return Vector3.Zero;   // nothing lopsided right now: chase normally, try again later
		Vector3 goal = NavGrid.FlankGoal;
		if (_flankTime > FlankTimeout || toTarget.LengthSquared() < FlankReleaseDistance * FlankReleaseDistance
			|| FlatDistanceSquared(goal, BodyPosition) < 4f)
		{
			if (_flankTime <= FlankTimeout)
				CallFromBehind(toTarget);
			_flanking = false;
			_route = NavGrid.Route.Direct;
			_repathTimer = 0f;
			return Vector3.Zero;
		}

		_repathTimer -= dt;
		bool arrived = FlatDistanceSquared(_waypoint, BodyPosition) < 0.09f;
		if (_repathTimer <= 0f || arrived)
		{
			_repathTimer = Stats.RepathInterval;
			if (!NavGrid.FindFlankRoute(BodyPosition, out _waypoint))
			{
				// No way round from here: just come at them.
				_flanking = false;
				_route = NavGrid.Route.Direct;
				_repathTimer = 0f;
				return Vector3.Zero;
			}
		}
		Vector3 to = _waypoint - BodyPosition;
		to.Y = 0f;
		return to.LengthSquared() > 0.01f ? to.Normalized() : Vector3.Zero;
	}

	/// <summary>Seconds between flank calls across all enemies, so a pack coming round doesn't spam it.</summary>
	public static float FlankCallGap { get; set; } = 0.9f;
	/// <summary>Volume of the call a flanker makes when it comes round behind the player.</summary>
	[Export] public float FlankCallVolumeDb { get; set; } = 2f;
	private static double _lastFlankCall = -10.0;
	/// <summary>How many flank calls have played (for the checks).</summary>
	public static int FlankCallCount { get; private set; }

	/// <summary>
	/// The fair warning: a flanker that ends up behind the player calls out once (its own voice,
	/// the DeathCue sound), so you hear it coming round before it bites.
	/// </summary>
	private void CallFromBehind(Vector3 toTarget)
	{
		if (DeathCue == EnemyCue.None || !IsInstanceValid(Target))
			return;
		Vector3 facing = -Target.GlobalBasis.Z;
		facing.Y = 0f;
		// toTarget points from us to the player; we're behind them if their facing points away from us.
		if (facing.Dot(toTarget) < 0.2f * toTarget.Length() * facing.Length())
			return;
		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastFlankCall < FlankCallGap / Mathf.Max((float)Engine.TimeScale, 0.01f))
			return;
		_lastFlankCall = now;
		FlankCallCount++;
		PlaceholderSfx.PlayAt(GetParent(), PlaceholderSfx.ForCue(DeathCue), BodyPosition + Vector3.Up, FlankCallVolumeDb, 0.1f);
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

	/// <summary>
	/// Knocks the body along the ground (a hammer hit). The push fades as the behaviour steers
	/// back to its own speed. Heavy enemies override this to shrug most of it off.
	/// </summary>
	public virtual void Shove(Vector3 push)
	{
		push *= Variant?.ShoveTaken ?? 1f;
		Velocity += new Vector3(push.X, 0f, push.Z);
		// Use real collision moves for a moment, so a shove can't slide it through cover.
		_noGlideTicks = 12;
	}

	private int _noGlideTicks;

	/// <summary>True when a plain slide along the ground is as good as a collision move this tick.</summary>
	private bool CanGlide(Vector3 velocity)
	{
		if (_noGlideTicks > 0)
		{
			_noGlideTicks--;
			return false;
		}
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

	/// <summary>
	/// A head-shot kill: the head bursts off in a shower of its own glow and a wet pop, and the
	/// corpse tumbles away without it.
	/// </summary>
	private void PopHead(Node parent)
	{
		if (_weakSpots == null)
			return;
		foreach (WeakSpot spot in _weakSpots)
		{
			Node3D head = spot.FollowTarget;
			Vector3 at = head?.GlobalPosition ?? spot.GlobalPosition;
			// Chunky glowing bits that hang a moment, plus a fast bright spray on top.
			ImpactEffects.SpawnSparks(parent, at, Vector3.Up, DeathSparkColor, HeadPopSparkAmount, 3.5f, 0.6f, 0.8f);
			ImpactEffects.SpawnSparks(parent, at, Vector3.Up, HeadPopSparkColor, HeadPopSparkAmount / 2, 1.5f, 0.35f, 1.4f);
			PlaceholderSfx.PlayAt(parent, PlaceholderSfx.HeadPop, at, HeadPopVolumeDb, 0.12f);
			if (head != null)
				head.Visible = false;
			HeadPops++;
			return;
		}
	}

	/// <summary>Head-shot kills that popped a head this session (for the self-check).</summary>
	public static int HeadPops { get; set; }

	protected virtual void OnDied()
	{
		GameEvents.Instance?.EmitSignal(GameEvents.SignalName.EnemyKilled, this);

		Node parent = GetParent();
		Vector3 center = GlobalPosition + Vector3.Up * EffectHeight;
		ImpactEffects.SpawnSparks(parent, center, Vector3.Up, DeathSparkColor, DeathSparkAmount);
		if (Health.LastHitWeakSpot)
			PopHead(parent);
		DieVariant(parent);
		if (DeathSound != null)
			PlaceholderSfx.PlayAt(parent, DeathSound, center, -6f, 0.15f);

		Vector3 away = IsInstanceValid(Target) ? GlobalPosition - Target.GlobalPosition : -GlobalBasis.Z;
		float groundY = Flying ? 0f : GlobalPosition.Y;
		Corpse.Spawn(parent, Visual, GlobalPosition, groundY, away, CorpseKnockSpeed, CorpseUpSpeed, CorpseHold, CorpseSink);

		QueueFree();
	}
}
