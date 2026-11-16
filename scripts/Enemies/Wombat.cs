using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// Charger. Plods after the player, then stops, winds up and charges in a straight line.
/// The direction locks late in the wind-up, so a sidestep after the lock makes it miss.
/// Hitting cover stuns it (a damage window); missing leaves it skidding and recovering.
/// Up close with no charge ready it headbutts using the shared MeleeAttack.
/// Optional children: "Visual/HeadPivot" (lowers and jabs), "Visual/Paw" (scrapes),
/// "Visual/Glow" (meshes under it brighten during the wind-up).
/// </summary>
public partial class Wombat : Enemy
{
	/// <summary>How much of a shove (hammer hit) actually moves it. Wombats are heavy.</summary>
	[Export] public float ShoveTaken { get; set; } = 0.2f;

	public override void Shove(Vector3 push) => base.Shove(push * ShoveTaken);

	[ExportGroup("Charge trigger")]
	/// <summary>Closest distance at which it will start a charge, in metres. Nearer than this it headbutts.</summary>
	[Export] public float ChargeMinDistance { get; set; } = 4f;
	/// <summary>Furthest distance at which it will start a charge, in metres.</summary>
	[Export] public float ChargeMaxDistance { get; set; } = 14f;
	/// <summary>Seconds after a charge ends before it can wind up again.</summary>
	[Export] public float ChargeCooldown { get; set; } = 3.5f;
	/// <summary>Seconds after spawning before its first charge.</summary>
	[Export] public float FirstChargeDelay { get; set; } = 1f;
	/// <summary>Physics layers that block its view of the player and stun it on impact (world only).</summary>
	[Export(PropertyHint.Layers3DPhysics)] public uint WorldMask { get; set; } = 1;
	/// <summary>Height of the sight ray above the feet, on both ends.</summary>
	[Export] public float SightHeight { get; set; } = 1f;

	[ExportGroup("Wind-up")]
	/// <summary>Seconds of wind-up before the charge. This is the player's warning.</summary>
	[Export] public float WindupTime { get; set; } = 0.8f;
	/// <summary>Fraction of the wind-up during which it keeps turning toward the player. After that the direction is locked.</summary>
	[Export(PropertyHint.Range, "0,1")] public float TrackFraction { get; set; } = 0.7f;
	/// <summary>How fast it turns toward the player while tracking.</summary>
	[Export] public float WindupTurnSpeed { get; set; } = 9f;
	[Export] public EnemyCue WindupCue { get; set; } = EnemyCue.Growl;
	/// <summary>Played the moment the charge direction locks: the "move now" cue.</summary>
	[Export] public EnemyCue LockCue { get; set; } = EnemyCue.Grunt;

	[ExportGroup("Charge")]
	[Export] public float ChargeSpeed { get; set; } = 12.6f;
	/// <summary>How quickly it reaches charge speed (m/s²).</summary>
	[Export] public float ChargeAcceleration { get; set; } = 90f;
	/// <summary>Charge ends after this many metres if nothing was hit.</summary>
	[Export] public float ChargeLength { get; set; } = 12f;
	[Export] public float ChargeDamage { get; set; } = 25f;
	/// <summary>How far ahead of its centre the charge connects, in metres.</summary>
	[Export] public float ChargeHitReach { get; set; } = 1.3f;
	/// <summary>Half the width of the charge hit, in metres. Sidestep further than this to dodge.</summary>
	[Export] public float ChargeHitHalfWidth { get; set; } = 0.95f;
	/// <summary>Horizontal speed added to the player on a charge hit (m/s).</summary>
	[Export] public float KnockbackSpeed { get; set; } = 8f;
	/// <summary>Upward speed added to the player on a charge hit (m/s).</summary>
	[Export] public float KnockbackUpSpeed { get; set; } = 3.5f;
	[Export] public EnemyCue ChargeCue { get; set; } = EnemyCue.Thump;

	[ExportGroup("After the charge")]
	/// <summary>Seconds stunned after ramming a wall or cover. The player's damage window.</summary>
	[Export] public float StunTime { get; set; } = 1.5f;
	/// <summary>Seconds it stays put after a charge that hit nothing solid.</summary>
	[Export] public float MissRecoverTime { get; set; } = 0.8f;
	/// <summary>How quickly it skids to a stop after a charge (m/s²).</summary>
	[Export] public float SkidDeceleration { get; set; } = 28f;
	/// <summary>How head-on a wall hit must be to stun (0 = any graze, 1 = dead straight).</summary>
	[Export(PropertyHint.Range, "0,1")] public float StunImpactDot { get; set; } = 0.35f;
	/// <summary>Speed it bounces back off the wall at (m/s).</summary>
	[Export] public float StunBounceSpeed { get; set; } = 3f;
	[Export] public Color ImpactSparkColor { get; set; } = new(1f, 0.85f, 0.5f);
	[Export] public int ImpactSparkAmount { get; set; } = 24;
	/// <summary>Dust kicked up behind it while it charges.</summary>
	[Export] public Color DustColor { get; set; } = new(0.9f, 0.78f, 0.6f);
	/// <summary>A player within this many metres feels the ground shake when it rams cover.</summary>
	[Export] public float SlamShakeRadius { get; set; } = 12f;
	/// <summary>Camera jolt at point-blank range, in degrees. It fades out to the radius.</summary>
	[Export] public float SlamShakeDegrees { get; set; } = 4f;

	[ExportGroup("Headbutt")]
	/// <summary>Seconds of wind-up before the close-range headbutt lands.</summary>
	[Export] public float HeadbuttWindupTime { get; set; } = 0.35f;
	[Export] public float HeadbuttRecoverTime { get; set; } = 0.45f;
	/// <summary>Hit still lands if the player is within AttackRange times this when it connects.</summary>
	[Export] public float HeadbuttReachMultiplier { get; set; } = 1.25f;
	[Export] public EnemyCue HeadbuttCue { get; set; } = EnemyCue.Thump;

	[ExportGroup("Animation")]
	[Export] public NodePath HeadPivotPath { get; set; } = "Visual/HeadPivot";
	[Export] public NodePath PawPath { get; set; } = "Visual/Paw";
	[Export] public NodePath GlowPath { get; set; } = "Visual/Glow";
	/// <summary>Leg nodes that swing forward first (one diagonal pair). Each pivots at its own origin and rests unrotated.</summary>
	[Export] public Godot.Collections.Array<NodePath> LegsA { get; set; } = new();
	/// <summary>The other diagonal pair: swings the opposite way.</summary>
	[Export] public Godot.Collections.Array<NodePath> LegsB { get; set; } = new();
	/// <summary>Leg swing either side of rest at full walking speed, in radians.</summary>
	[Export] public float LegSwingAngle { get; set; } = 0.4f;
	/// <summary>Leg swing while charging, in radians.</summary>
	[Export] public float ChargeLegSwingAngle { get; set; } = 0.65f;
	/// <summary>Legs are left alone when the target is further away than this, in metres.</summary>
	[Export] public float LegSwingRange { get; set; } = 25f;
	/// <summary>How fast the legs come back to rest when it stops, in radians per second.</summary>
	[Export] public float LegSettleSpeed { get; set; } = 9f;
	/// <summary>Bob height while walking, in metres.</summary>
	[Export] public float BobHeight { get; set; } = 0.05f;
	/// <summary>Bobs per metre travelled.</summary>
	[Export] public float BobFrequency { get; set; } = 0.8f;
	/// <summary>How far it rears back during the wind-up, in radians (nose up).</summary>
	[Export] public float WindupRearAngle { get; set; } = 0.2f;
	/// <summary>How far it shifts back on its haunches during the wind-up, in metres.</summary>
	[Export] public float WindupPullback { get; set; } = 0.3f;
	/// <summary>Side-to-side shake at the end of the wind-up, in metres.</summary>
	[Export] public float WindupShake { get; set; } = 0.05f;
	/// <summary>Nose-down lean once locked and while charging, in radians.</summary>
	[Export] public float ChargeLeanAngle { get; set; } = 0.16f;
	/// <summary>How far the head drops once locked and while charging, in metres.</summary>
	[Export] public float HeadLowerDistance { get; set; } = 0.12f;
	/// <summary>Paw scrapes per second during the wind-up.</summary>
	[Export] public float PawScrapeRate { get; set; } = 5f;
	/// <summary>Constant sideways lean while stunned, in radians.</summary>
	[Export] public float StunLeanAngle { get; set; } = 0.2f;
	/// <summary>Sway around that lean while stunned, in radians.</summary>
	[Export] public float StunWobbleAngle { get; set; } = 0.14f;
	/// <summary>How far the body sags while stunned, in metres.</summary>
	[Export] public float StunSag { get; set; } = 0.1f;
	/// <summary>Wobbles per second while stunned.</summary>
	[Export] public float StunWobbleRate { get; set; } = 1.6f;
	/// <summary>Head droop while stunned, in metres.</summary>
	[Export] public float StunHeadDroop { get; set; } = 0.3f;
	/// <summary>Glow emission strength at rest.</summary>
	[Export] public float GlowIdleEnergy { get; set; } = 1.5f;
	/// <summary>Glow emission strength at the end of the wind-up and during the charge.</summary>
	[Export] public float GlowChargeEnergy { get; set; } = 4f;
	/// <summary>Glow emission strength while stunned. Below the idle value the patches also darken.</summary>
	[Export] public float GlowStunEnergy { get; set; } = 0.2f;

	private enum State { Chase, Windup, Charge, Skid, Stunned, HeadbuttWindup, HeadbuttRecover }

	/// <summary>Current behaviour state, for debugging and test scenes.</summary>
	public string StateName => _state.ToString();

	private MeleeAttack _melee;
	private Node3D _headPivot;
	private Node3D _paw;
	private StandardMaterial3D _glowMaterial;
	private Color _glowBaseColor;
	private Vector3 _visualRest;
	private Vector3 _headRest;
	private Vector3 _pawRest;
	private Node3D[] _legsA = System.Array.Empty<Node3D>();
	private Node3D[] _legsB = System.Array.Empty<Node3D>();
	private float _legAngle;
	private float _legShown;

	private State _state = State.Chase;
	private float _stateTimer;
	private float _stateTime;       // seconds spent in the current state
	private float _chargeCooldownTimer;
	private Vector3 _chargeDirection;
	private bool _directionLocked;
	private float _chargeTravelled;
	private float _bobPhase;
	private float _glowEnergy;
	private float _glowShown = float.MinValue;
	private Vector3 _pawOffset;

	public override void _Ready()
	{
		base._Ready();
		_melee = GetNode<MeleeAttack>("MeleeAttack");
		_melee.Damage = Stats.AttackDamage;
		_melee.Range = Stats.AttackRange;
		_melee.Cooldown = Stats.AttackCooldown;

		_headPivot = GetNodeOrNull<Node3D>(HeadPivotPath);
		_paw = GetNodeOrNull<Node3D>(PawPath);
		_visualRest = Visual.Position;
		if (_headPivot != null)
			_headRest = _headPivot.Position;
		if (_paw != null)
			_pawRest = _paw.Position;
		_legsA = ResolveLegs(LegsA);
		_legsB = ResolveLegs(LegsB);

		SetUpGlow();
		_chargeCooldownTimer = FirstChargeDelay;
		_bobPhase = GD.Randf() * Mathf.Tau;
	}

	private Node3D[] ResolveLegs(Godot.Collections.Array<NodePath> paths)
	{
		if (paths == null || paths.Count == 0)
			return System.Array.Empty<Node3D>();
		var legs = new List<Node3D>(paths.Count);
		foreach (NodePath path in paths)
			if (GetNodeOrNull<Node3D>(path) is { } leg)
				legs.Add(leg);
		return legs.ToArray();
	}

	/// <summary>Gives this wombat its own glow material, so brightening one leaves the others alone.</summary>
	private void SetUpGlow()
	{
		_glowEnergy = GlowIdleEnergy;
		Node glowRoot = GetNodeOrNull(GlowPath);
		if (glowRoot == null)
			return;

		var meshes = new List<MeshInstance3D>();
		foreach (Node child in glowRoot.GetChildren())
			if (child is MeshInstance3D mesh)
				meshes.Add(mesh);
		if (meshes.Count == 0 || meshes[0].GetActiveMaterial(0) is not StandardMaterial3D source)
			return;

		_glowMaterial = (StandardMaterial3D)source.Duplicate();
		_glowBaseColor = _glowMaterial.AlbedoColor;
		foreach (MeshInstance3D mesh in meshes)
			mesh.MaterialOverride = _glowMaterial;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (Health.IsDead)
			return;
		BeginTick();
		Tick(dt);
		EndTick();
	}

	private void Tick(float dt)
	{
		_stateTime += dt;
		if (_chargeCooldownTimer > 0f && _state == State.Chase)
			_chargeCooldownTimer -= dt;

		if (!HasLiveTarget)
		{
			if (_state != State.Chase)
				Enter(State.Chase);
			Steer(Vector3.Zero, dt);
			return;
		}

		Vector3 toTarget = TargetPosition - BodyPosition;
		toTarget.Y = 0f;
		float distance = toTarget.Length();
		Vector3 toTargetDir = distance > 0.001f ? toTarget / distance : -Visual.GlobalBasis.Z;

		switch (_state)
		{
			case State.Chase:
				Chase(dt, distance, toTargetDir);
				break;
			case State.Windup:
				Windup(dt, toTargetDir);
				break;
			case State.Charge:
				Charge(dt);
				break;
			case State.Skid:
			case State.Stunned:
			case State.HeadbuttRecover:
				_stateTimer -= dt;
				SlideToStop(dt);
				if (_stateTimer <= 0f)
					Enter(State.Chase);
				break;
			case State.HeadbuttWindup:
				_stateTimer -= dt;
				FaceDirection(toTargetDir, dt, 10f);
				SlideToStop(dt);
				if (_stateTimer <= 0f)
					Headbutt(distance);
				break;
		}
	}

	private void Enter(State state, float duration = 0f)
	{
		SetDust(state == State.Charge);
		_state = state;
		_stateTimer = duration;
		_stateTime = 0f;
	}

	private void Chase(float dt, float distance, Vector3 toTargetDir)
	{
		bool grounded = Grounded;

		if (grounded && _chargeCooldownTimer <= 0f
			&& distance >= ChargeMinDistance && distance <= ChargeMaxDistance && CanSeeTarget())
		{
			_directionLocked = false;
			_chargeDirection = toTargetDir;
			Enter(State.Windup, WindupTime);
			PlayCue(WindupCue, -3f);
			SlideToStop(dt);
			return;
		}

		if (distance <= Stats.AttackRange)
		{
			if (grounded && _melee.IsReady)
			{
				_melee.StartCooldown();
				Enter(State.HeadbuttWindup, HeadbuttWindupTime);
			}
			// In reach: hold ground instead of shoving into the player.
			Steer(Separation(), dt);
			FaceDirection(toTargetDir, dt);
			return;
		}

		float stopDistance = Mathf.Max(distance - Stats.AttackRange * 0.9f, 0f);
		float speed = Mathf.Min(Stats.MoveSpeed, Mathf.Sqrt(2f * Stats.Acceleration * stopDistance) + 0.5f);
		Vector3 direction = ChaseDirection(dt);
		Steer(direction * speed + Separation(), dt);
		FaceDirection(direction, dt, 6f);
	}

	private void Windup(float dt, Vector3 toTargetDir)
	{
		_stateTimer -= dt;

		if (!_directionLocked)
		{
			_chargeDirection = toTargetDir;
			FaceDirection(toTargetDir, dt, WindupTurnSpeed);
			if (_stateTime >= WindupTime * TrackFraction)
			{
				// Lock: from here on it is committed, and the player can step out of the line.
				_directionLocked = true;
				FaceDirection(_chargeDirection, 1f, 100f);
				PlayCue(LockCue, 0f);
			}
		}

		SlideToStop(dt);

		if (_stateTimer <= 0f)
		{
			_directionLocked = true;
			_chargeTravelled = 0f;
			Enter(State.Charge);
			PlayCue(ChargeCue, 0f);
		}
	}

	private void Charge(float dt)
	{
		// No steering: always drive along the locked line, whatever the slide did last frame.
		Vector3 velocity = Velocity;
		Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z)
			.MoveToward(_chargeDirection * ChargeSpeed, ChargeAcceleration * dt);
		velocity.X = horizontal.X;
		velocity.Z = horizontal.Z;
		if (!IsOnFloor())
			velocity.Y -= Gravity * dt;
		Velocity = velocity;

		Vector3 before = GlobalPosition;
		Move();
		Vector3 moved = GlobalPosition - before;
		moved.Y = 0f;
		_chargeTravelled += moved.Length();

		bool rammedPlayer = false;
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision3D collision = GetSlideCollision(i);
			if (collision.GetCollider() == Target)
			{
				rammedPlayer = true;
				continue;
			}

			Vector3 normal = collision.GetNormal();
			bool isWall = Mathf.Abs(normal.Y) < 0.5f;
			bool isWorld = IsWorld(collision.GetCollider());
			if (isWall && isWorld && -normal.Dot(_chargeDirection) >= StunImpactDot)
			{
				HitWall(collision.GetPosition(), normal);
				return;
			}
		}

		if (rammedPlayer || PlayerInChargePath())
		{
			HitPlayer();
			return;
		}

		// The time limit covers a charge that is blocked without ramming anything head-on.
		if (_chargeTravelled >= ChargeLength || _stateTime >= 1.5f * ChargeLength / Mathf.Max(ChargeSpeed, 0.01f))
			EndCharge(State.Skid, MissRecoverTime);
	}

	/// <summary>Walls and cover. The arena is built from CSG boxes, which collide but aren't physics bodies.</summary>
	private bool IsWorld(GodotObject collider) => collider switch
	{
		CollisionObject3D body => (body.CollisionLayer & WorldMask) != 0,
		CsgShape3D csg => (csg.CollisionLayer & WorldMask) != 0,
		_ => true,
	};

	private bool PlayerInChargePath()
	{
		Vector3 offset = Target.GlobalPosition - GlobalPosition;
		if (Mathf.Abs(offset.Y) > 1.6f)
			return false;
		offset.Y = 0f;
		float ahead = offset.Dot(_chargeDirection);
		float aside = (offset - _chargeDirection * ahead).Length();
		return ahead > -0.2f && ahead <= ChargeHitReach && aside <= ChargeHitHalfWidth;
	}

	/// <summary>One hit per charge: the charge ends here and it skids on past.</summary>
	private void HitPlayer()
	{
		TargetHealth.ApplyDamage(ChargeDamage, GlobalPosition);
		if (Target is CharacterBody3D body)
		{
			// Set rather than add, so several wombats hitting at once can't stack the player
			// into orbit: the strongest single shove wins.
			Vector3 v = body.Velocity;
			Vector3 horizontal = new(v.X, 0f, v.Z);
			float along = horizontal.Dot(_chargeDirection);
			if (along < KnockbackSpeed)
				horizontal += _chargeDirection * (KnockbackSpeed - along);
			body.Velocity = new Vector3(horizontal.X, Mathf.Max(v.Y, KnockbackUpSpeed), horizontal.Z);
		}
		PlayCue(EnemyCue.Thump, 2f);
		EndCharge(State.Skid, MissRecoverTime);
	}

	private CpuParticles3D _dust;

	/// <summary>Starts or stops the charge's dust trail (built the first time it charges).</summary>
	private void SetDust(bool on)
	{
		if (_dust == null)
		{
			if (!on)
				return;
			_dust = new CpuParticles3D
			{
				Name = "ChargeDust",
				Emitting = false,
				Amount = 36,
				Lifetime = 0.7f,
				// World-space particles, so the cloud hangs behind it as it runs on.
				LocalCoords = false,
				Position = new Vector3(0f, 0.15f, 0f),
				EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(0.35f, 0.05f, 0.35f),
				Direction = Vector3.Up,
				Spread = 70f,
				Gravity = new Vector3(0f, 1.2f, 0f),
				InitialVelocityMin = 0.4f,
				InitialVelocityMax = 1.4f,
				ScaleAmountMin = 1.8f,
				ScaleAmountMax = 3.4f,
				Mesh = new BoxMesh
				{
					Size = Vector3.One * 0.16f,
					Material = new StandardMaterial3D
					{
						ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
						AlbedoColor = new Color(DustColor, 0.7f),
						Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					},
				},
			};
			AddChild(_dust);
		}
		_dust.Emitting = on;
	}

	/// <summary>Shakes the player's view when it rams something near them.</summary>
	private void SlamShake(Vector3 point)
	{
		if (!IsInstanceValid(Target))
			return;
		float distance = point.DistanceTo(Target.GlobalPosition);
		if (distance >= SlamShakeRadius)
			return;
		var camera = Target.GetNodeOrNull<Player.CameraRecoil>("Head/Camera3D");
		if (camera == null)
			return;
		float strength = SlamShakeDegrees * (1f - distance / SlamShakeRadius);
		camera.AddKick((float)GD.RandRange(-strength, strength), (float)GD.RandRange(-strength, strength));
		SlamShakes++;
	}

	/// <summary>Cover slams that shook the player's view this session (for the self-check).</summary>
	public static int SlamShakes { get; set; }

	private void HitWall(Vector3 point, Vector3 normal)
	{
		SlamShake(point);
		ImpactEffects.SpawnSparks(GetParent(), point, normal, ImpactSparkColor, ImpactSparkAmount);
		PlaceholderSfx.PlayAt(GetParent(), PlaceholderSfx.Thump, point, 4f, 0.05f);
		Velocity = -_chargeDirection * StunBounceSpeed;
		EndCharge(State.Stunned, StunTime);
	}

	private void EndCharge(State next, float duration)
	{
		_chargeCooldownTimer = ChargeCooldown;
		_melee.StartCooldown();
		Enter(next, duration);
	}

	private void Headbutt(float distance)
	{
		Enter(State.HeadbuttRecover, HeadbuttRecoverTime);
		if (distance <= Stats.AttackRange * HeadbuttReachMultiplier && _melee.ApplyTo(TargetHealth))
			PlayCue(HeadbuttCue, -4f);
	}

	/// <summary>Brakes to a stop without steering (skids, stuns and wind-ups).</summary>
	private void SlideToStop(float dt)
	{
		Vector3 velocity = Velocity;
		Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z).MoveToward(Vector3.Zero, SkidDeceleration * dt);
		velocity.X = horizontal.X;
		velocity.Z = horizontal.Z;
		if (!IsOnFloor())
			velocity.Y -= Gravity * dt;
		Velocity = velocity;
		Move();
	}

	private bool CanSeeTarget()
	{
		Vector3 up = Vector3.Up * SightHeight;
		var query = PhysicsRayQueryParameters3D.Create(GlobalPosition + up, Target.GlobalPosition + up, WorldMask);
		return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	private void PlayCue(EnemyCue cue, float volumeDb)
	{
		AudioStream sound = PlaceholderSfx.ForCue(cue);
		if (sound != null)
			PlaceholderSfx.PlayAt(GetParent(), sound, GlobalPosition + Vector3.Up * EffectHeight, volumeDb, 0.06f);
	}

	protected override void OnDied()
	{
		// Big burst: a second shower from the head on top of the shared one.
		if (_headPivot != null)
			ImpactEffects.SpawnSparks(GetParent(), _headPivot.GlobalPosition, Vector3.Up, ImpactSparkColor, DeathSparkAmount / 2);
		base.OnDied();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		Vector3 offset = Vector3.Zero;   // Visual offset in its own facing space (z+ = backwards)
		float pitch = 0f;                // + = nose up
		float roll = 0f;
		float headDrop = 0f;
		float headThrust = 0f;           // + = forwards
		Vector3 pawOffset = Vector3.Zero;
		float glowTarget = GlowIdleEnergy;
		float legTarget = 0f;

		switch (_state)
		{
			case State.Chase:
				float speed = new Vector2(Velocity.X, Velocity.Z).Length();
				_bobPhase += speed * BobFrequency * Mathf.Pi * dt;
				float amount = Mathf.Min(speed / Mathf.Max(Stats.MoveSpeed, 0.01f), 1f);
				offset.Y = (Mathf.Sin(_bobPhase) * 0.5f + 0.5f) * BobHeight * amount;
				roll = Mathf.Sin(_bobPhase * 0.5f) * 0.04f * amount;
				legTarget = Mathf.Sin(_bobPhase * 0.5f) * LegSwingAngle * amount;
				break;

			case State.Windup:
				float t = Mathf.Clamp(_stateTime / Mathf.Max(WindupTime, 0.001f), 0f, 1f);
				float rear = Mathf.SmoothStep(0f, 1f, Mathf.Min(t / Mathf.Max(TrackFraction, 0.01f), 1f));
				glowTarget = Mathf.Lerp(GlowIdleEnergy, GlowChargeEnergy, t * t);
				offset.X = Mathf.Sin(_stateTime * 55f) * WindupShake * t;
				offset.Z = WindupPullback * rear;
				if (_directionLocked)
				{
					// Committed: drop the head and lean into it.
					pitch = -ChargeLeanAngle;
					headDrop = HeadLowerDistance;
					glowTarget = GlowChargeEnergy;
				}
				else
				{
					pitch = WindupRearAngle * rear;
					float scrape = Mathf.PosMod(_stateTime * PawScrapeRate, 1f);
					// Reach forward and up, then drag back along the ground.
					pawOffset = scrape < 0.4f
						? new Vector3(0f, Mathf.Sin(scrape / 0.4f * Mathf.Pi) * 0.22f, -0.3f * (scrape / 0.4f))
						: new Vector3(0f, 0f, Mathf.Lerp(-0.3f, 0.12f, (scrape - 0.4f) / 0.6f));
				}
				break;

			case State.Charge:
				pitch = -ChargeLeanAngle;
				headDrop = HeadLowerDistance;
				glowTarget = GlowChargeEnergy;
				offset.Y = Mathf.Abs(Mathf.Sin(_stateTime * 22f)) * 0.06f;
				legTarget = Mathf.Sin(_stateTime * 22f) * ChargeLegSwingAngle;
				break;

			case State.Skid:
				float skid = 1f - Mathf.Clamp(_stateTime / 0.35f, 0f, 1f);
				pitch = WindupRearAngle * 0.8f * skid;   // digs its heels in
				glowTarget = Mathf.Lerp(GlowIdleEnergy, GlowChargeEnergy, skid);
				break;

			case State.Stunned:
				float fade = Mathf.Clamp(_stateTimer / 0.3f, 0f, 1f);   // settle before it recovers
				float wobble = Mathf.Sin(_stateTime * StunWobbleRate * Mathf.Tau);
				// Slumped and listing to one side, swaying around that lean.
				roll = (StunLeanAngle + wobble * StunWobbleAngle) * fade;
				pitch = -0.12f * fade;
				headDrop = StunHeadDroop * fade;
				offset.X = wobble * 0.08f * fade;
				offset.Y = -StunSag * fade;
				glowTarget = GlowStunEnergy;
				break;

			case State.HeadbuttWindup:
				float pull = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_stateTime / Mathf.Max(HeadbuttWindupTime, 0.001f), 0f, 1f));
				headThrust = -0.22f * pull;
				headDrop = 0.1f * pull;
				pitch = 0.08f * pull;
				break;

			case State.HeadbuttRecover:
				const float jabTime = 0.06f;
				float jab = _stateTime < jabTime
					? _stateTime / jabTime
					: 1f - Mathf.Clamp((_stateTime - jabTime) / Mathf.Max(HeadbuttRecoverTime - jabTime, 0.01f), 0f, 1f);
				headThrust = 0.5f * jab;
				pitch = -0.1f * jab;
				break;
		}

		float blend = 1f - Mathf.Exp(-22f * dt);
		Vector3 rotation = Visual.Rotation;
		rotation.X = Mathf.Lerp(rotation.X, pitch, blend);
		rotation.Z = Mathf.Lerp(rotation.Z, roll, blend);
		Visual.Rotation = rotation;

		Basis facing = Basis.FromEuler(new Vector3(0f, rotation.Y, 0f));
		Visual.Position = Visual.Position.Lerp(_visualRest + facing * offset, blend);

		if (_headPivot != null)
			_headPivot.Position = _headPivot.Position.Lerp(_headRest + new Vector3(0f, -headDrop, -headThrust), blend);
		// The paw and the glow sit still most of the time: only write them when they change.
		if (_paw != null && pawOffset.DistanceSquaredTo(_pawOffset) > 1e-8f)
		{
			_pawOffset = pawOffset;
			_paw.Position = _pawRest + pawOffset;
		}

		AnimateLegs(legTarget, dt);

		if (_glowMaterial != null)
		{
			_glowEnergy = Mathf.Lerp(_glowEnergy, glowTarget, 1f - Mathf.Exp(-18f * dt));
			if (Mathf.Abs(_glowEnergy - _glowShown) < 0.002f)
				return;
			_glowShown = _glowEnergy;
			_glowMaterial.EmissionEnergyMultiplier = _glowEnergy;
			// Emission alone can't go darker than the lit colour, so fade the colour too when dimmed.
			float dim = Mathf.Clamp(_glowEnergy / Mathf.Max(GlowIdleEnergy, 0.01f), 0.25f, 1f);
			_glowMaterial.AlbedoColor = _glowBaseColor * new Color(dim, dim, dim);
		}
	}

	/// <summary>Swings the legs in diagonal pairs: one angle per frame, group B mirrored.</summary>
	private void AnimateLegs(float target, float dt)
	{
		if (_legsA.Length == 0 && _legsB.Length == 0)
			return;
		if (target == 0f && _legAngle == 0f)
			return;

		float dx = TargetPosition.X - BodyPosition.X, dz = TargetPosition.Z - BodyPosition.Z;
		if (dx * dx + dz * dz > LegSwingRange * LegSwingRange)
			return;

		// The charge gallop is quick: let the legs keep up with it.
		float rate = _state == State.Charge ? 40f : LegSettleSpeed;
		_legAngle = Mathf.MoveToward(_legAngle, target, rate * dt);
		if (Mathf.Abs(_legAngle - _legShown) < 0.01f && _legAngle != 0f)
			return;
		_legShown = _legAngle;

		var a = new Vector3(_legAngle, 0f, 0f);
		var b = new Vector3(-_legAngle, 0f, 0f);
		foreach (Node3D leg in _legsA)
			leg.Rotation = a;
		foreach (Node3D leg in _legsB)
			leg.Rotation = b;
	}
}
