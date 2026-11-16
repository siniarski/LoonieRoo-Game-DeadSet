using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// Shared behaviour for enemies that run at the player and hit them up close:
/// chase, telegraphed wind-up, strike, short recovery. Each enemy type tunes it in its scene
/// (pecks, bites, kicks, hops, flying) instead of needing its own script.
/// Optional child "Visual/Striker" is the part that pulls back and jabs (a head, a leg).
/// </summary>
public partial class MeleeChaser : Enemy
{
	[ExportGroup("Attack")]
	/// <summary>Seconds of visible wind-up before the hit lands. This is the player's warning.</summary>
	[Export] public float WindupTime { get; set; } = 0.25f;
	/// <summary>Seconds it stays put after striking before chasing again.</summary>
	[Export] public float RecoverTime { get; set; } = 0.3f;
	/// <summary>Hit still lands if the player is within AttackRange times this when it connects.</summary>
	[Export] public float ReachMultiplier { get; set; } = 1.4f;
	/// <summary>Forward leap speed at wind-up start (0 = plant feet and strike).</summary>
	[Export] public float LungeSpeed { get; set; }
	[Export] public float LungeUpSpeed { get; set; }
	[Export] public EnemyCue AttackCue { get; set; } = EnemyCue.Peck;

	[ExportGroup("Striker")]
	[Export] public NodePath StrikerPath { get; set; } = "Visual/Striker";
	/// <summary>Local offset the striker pulls back to during the wind-up.</summary>
	[Export] public Vector3 StrikerPullback { get; set; } = new(0f, 0.06f, 0.25f);
	/// <summary>Local offset the striker jabs out to when the hit lands.</summary>
	[Export] public Vector3 StrikerThrust { get; set; } = new(0f, -0.04f, -0.4f);
	/// <summary>Pitch added to the striker at the end of the wind-up, in radians (+ = tips back, lifting its far end).</summary>
	[Export] public float StrikerPullbackPitch { get; set; }
	/// <summary>Pitch added to the striker when the hit lands, in radians (- = tips forward and down).</summary>
	[Export] public float StrikerThrustPitch { get; set; }

	[ExportGroup("Gait")]
	/// <summary>Bob height while running, in metres. Keep it small on common enemies so heads stay level.</summary>
	[Export] public float BobHeight { get; set; } = 0.03f;
	/// <summary>Bobs per metre travelled.</summary>
	[Export] public float BobFrequency { get; set; } = 1.2f;
	/// <summary>Hops (bounces off the ground) rather than a smooth bob.</summary>
	[Export] public bool Hops { get; set; }
	/// <summary>Idle hover bob for flyers, in metres.</summary>
	[Export] public float HoverBob { get; set; } = 0.12f;

	[ExportGroup("Legs")]
	/// <summary>Leg nodes that swing forward first. Each pivots at its own origin (the hip) and rests unrotated.</summary>
	[Export] public Godot.Collections.Array<NodePath> LegsA { get; set; } = new();
	/// <summary>Leg nodes that swing the opposite way (the other leg, or the other diagonal pair).</summary>
	[Export] public Godot.Collections.Array<NodePath> LegsB { get; set; } = new();
	/// <summary>Swing either side of rest at full speed, in radians. With Hops, how far both legs trail back at the top of the hop.</summary>
	[Export] public float LegSwingAngle { get; set; } = 0.55f;
	/// <summary>Legs are left alone when the target is further away than this, in metres.</summary>
	[Export] public float LegSwingRange { get; set; } = 25f;
	/// <summary>How fast the legs come back to rest when it stops, in radians per second.</summary>
	[Export] public float LegSettleSpeed { get; set; } = 9f;

	private enum State { Chase, Windup, Recover }

	private MeleeAttack _melee;
	private Node3D _striker;
	private Vector3 _strikerRest;
	private Vector3 _visualRest;
	private State _state = State.Chase;
	private float _stateTimer;
	private float _bobPhase;
	private float _flatSpeed;
	private Vector3 _visualOffset;
	private Vector3 _strikerOffset;
	private Vector3 _strikerRestRotation;
	private float _strikerPitch;
	private Node3D[] _legsA = System.Array.Empty<Node3D>();
	private Node3D[] _legsB = System.Array.Empty<Node3D>();
	private float _gaitAmount;
	private float _legAngle;
	private float _legShown;

	public override void _Ready()
	{
		base._Ready();
		_melee = GetNode<MeleeAttack>("MeleeAttack");
		_melee.Damage = Stats.AttackDamage;
		_melee.Range = Stats.AttackRange;
		_melee.Cooldown = Stats.AttackCooldown;

		_striker = StrikerPath == null || StrikerPath.IsEmpty ? null : GetNodeOrNull<Node3D>(StrikerPath);
		if (_striker != null)
		{
			_strikerRest = _striker.Position;
			_strikerRestRotation = _striker.Rotation;
		}
		_legsA = ResolveLegs(LegsA);
		_legsB = ResolveLegs(LegsB);
		_visualRest = Visual.Position;
		_bobPhase = GD.Randf() * Mathf.Tau;
	}

	private Node3D[] ResolveLegs(Godot.Collections.Array<NodePath> paths)
	{
		if (paths == null || paths.Count == 0)
			return System.Array.Empty<Node3D>();
		var legs = new System.Collections.Generic.List<Node3D>(paths.Count);
		foreach (NodePath path in paths)
			if (GetNodeOrNull<Node3D>(path) is { } leg)
				legs.Add(leg);
		return legs.ToArray();
	}

	/// <summary>A shove mid wind-up knocks it out of the attack (the attack cooldown still runs).</summary>
	public override void Shove(Vector3 push)
	{
		base.Shove(push);
		if (_state == State.Windup)
		{
			_state = State.Chase;
			AttackSlots.Release(this);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (Health.IsDead)
			return;
		BeginTick();
		Tick(dt);
		EndTick();
		Vector3 velocity = Velocity;
		_flatSpeed = Mathf.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
	}

	private void Tick(float dt)
	{
		if (!HasLiveTarget)
		{
			_state = State.Chase;
			AttackSlots.Release(this);
			Steer(Vector3.Zero, dt);
			return;
		}

		Vector3 toTarget = TargetPosition - BodyPosition;
		toTarget.Y = 0f;
		float distance = toTarget.Length();
		Vector3 toTargetDir = toTarget / Mathf.Max(distance, 0.001f);
		bool lunging = LungeSpeed > 0f;

		switch (_state)
		{
			case State.Windup:
				_stateTimer -= dt;
				if (!lunging)
					FaceDirection(toTargetDir, dt, 10f);
				if (_stateTimer <= 0f)
					Strike(distance);
				Steer(Vector3.Zero, dt, allowSteering: !lunging);
				return;

			case State.Recover:
				_stateTimer -= dt;
				bool landed = !lunging || Flying || Grounded;
				if (_stateTimer <= 0f && landed)
				{
					_state = State.Chase;
					AttackSlots.Release(this);
				}
				Steer(Vector3.Zero, dt, allowSteering: !lunging || Grounded);
				return;
		}

		if (distance <= Stats.AttackRange && _melee.IsReady && (Flying || Grounded) && AttackSlots.TryAcquire(this))
		{
			StartWindup(toTargetDir);
			Steer(Vector3.Zero, dt, allowSteering: !lunging);
			return;
		}

		// In reach but cooling down or waiting for an attack slot: hold ground instead of
		// crowding into the player's face.
		if (distance <= Stats.AttackRange)
		{
			Steer(Separation(), dt);
			FaceDirection(toTargetDir, dt);
			return;
		}

		// Brake early enough to stop at reach rather than skidding into the player.
		float stopDistance = Mathf.Max(distance - Stats.AttackRange * 0.9f, 0f);
		float speed = Mathf.Min(Stats.MoveSpeed, Mathf.Sqrt(2f * Stats.Acceleration * stopDistance) + 0.5f);

		Vector3 direction = ChaseDirection(dt);
		Steer(direction * speed + Separation(), dt);
		FaceDirection(direction, dt);
	}

	private void StartWindup(Vector3 direction)
	{
		_state = State.Windup;
		_stateTimer = WindupTime;
		_melee.StartCooldown();
		FaceDirection(direction, 1f, 100f);
		if (LungeSpeed > 0f)
			Velocity = direction * LungeSpeed + Vector3.Up * LungeUpSpeed;
	}

	private void Strike(float distance)
	{
		_state = State.Recover;
		_stateTimer = RecoverTime;
		if (distance <= Stats.AttackRange * ReachMultiplier && _melee.ApplyTo(TargetHealth))
		{
			AudioStream sound = PlaceholderSfx.ForCue(AttackCue);
			if (sound != null)
				PlaceholderSfx.PlayAt(GetParent(), sound, BodyPosition + Vector3.Up * EffectHeight, -6f);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		AnimateGait(dt);
		AnimateLegs(dt);
		AnimateStriker();
	}

	private void AnimateGait(float dt)
	{
		float speed = _flatSpeed;
		Vector3 offset = Vector3.Zero;
		_gaitAmount = 0f;

		if (Flying)
		{
			_bobPhase += dt * 2.5f;
			offset.Y = Mathf.Sin(_bobPhase) * HoverBob;
		}
		else if (_state == State.Chase && Grounded)
		{
			_bobPhase += speed * BobFrequency * Mathf.Pi * dt;
			float amount = Mathf.Min(speed / Mathf.Max(Stats.MoveSpeed, 0.01f), 1f);
			float wave = Hops ? Mathf.Abs(Mathf.Sin(_bobPhase)) : Mathf.Sin(_bobPhase) * 0.5f + 0.5f;
			offset.Y = wave * BobHeight * amount;
			_gaitAmount = amount;
		}

		// Only touch the node when it actually moves: every write re-sends the whole model to the renderer.
		if (offset.DistanceSquaredTo(_visualOffset) < 1e-6f)
			return;
		_visualOffset = offset;
		Visual.Position = _visualRest + offset;
	}

	/// <summary>
	/// Swings the legs with the run: one angle per enemy per frame, shared by every leg
	/// (group B mirrored). A stride is two bobs, so the feet keep time with the body.
	/// Hoppers push both legs back together as they leave the ground.
	/// </summary>
	private void AnimateLegs(float dt)
	{
		if (_legsA.Length == 0 && _legsB.Length == 0)
			return;

		float target = 0f;
		if (_gaitAmount > 0f)
			target = (Hops ? -Mathf.Abs(Mathf.Sin(_bobPhase)) : Mathf.Sin(_bobPhase * 0.5f)) * LegSwingAngle * _gaitAmount;
		if (target == 0f && _legAngle == 0f)
			return;   // standing still with its legs at rest: nothing to do

		// Too far off to see the feet move.
		float dx = TargetPosition.X - BodyPosition.X, dz = TargetPosition.Z - BodyPosition.Z;
		if (dx * dx + dz * dz > LegSwingRange * LegSwingRange)
			return;

		_legAngle = Mathf.MoveToward(_legAngle, target, LegSettleSpeed * dt);
		if (Mathf.Abs(_legAngle - _legShown) < 0.01f && _legAngle != 0f)
			return;
		_legShown = _legAngle;

		var a = new Vector3(_legAngle, 0f, 0f);
		var b = Hops ? a : new Vector3(-_legAngle, 0f, 0f);
		foreach (Node3D leg in _legsA)
			leg.Rotation = a;
		foreach (Node3D leg in _legsB)
			leg.Rotation = b;
	}

	private void AnimateStriker()
	{
		if (_striker == null)
			return;

		Vector3 offset = Vector3.Zero;
		float pitch = 0f;
		if (_state == State.Windup)
		{
			float t = 1f - Mathf.Clamp(_stateTimer / Mathf.Max(WindupTime, 0.001f), 0f, 1f);
			float eased = Mathf.SmoothStep(0f, 1f, t);
			offset = StrikerPullback * eased;
			pitch = StrikerPullbackPitch * eased;
		}
		else if (_state == State.Recover)
		{
			// Snap out to the thrust, then ease back to rest over the recovery.
			float elapsed = RecoverTime - _stateTimer;
			const float jabTime = 0.05f;
			if (elapsed < jabTime)
			{
				float jab = elapsed / jabTime;
				offset = StrikerPullback.Lerp(StrikerThrust, jab);
				pitch = Mathf.Lerp(StrikerPullbackPitch, StrikerThrustPitch, jab);
			}
			else
			{
				float back = Mathf.Clamp((elapsed - jabTime) / Mathf.Max(RecoverTime - jabTime, 0.01f), 0f, 1f);
				offset = StrikerThrust.Lerp(Vector3.Zero, back);
				pitch = Mathf.Lerp(StrikerThrustPitch, 0f, back);
			}
		}

		if (Mathf.Abs(pitch - _strikerPitch) > 0.002f || (pitch == 0f && _strikerPitch != 0f))
		{
			_strikerPitch = pitch;
			_striker.Rotation = new Vector3(_strikerRestRotation.X + pitch, _strikerRestRotation.Y, _strikerRestRotation.Z);
		}

		if (offset.DistanceSquaredTo(_strikerOffset) < 1e-6f)
			return;
		_strikerOffset = offset;
		_striker.Position = _strikerRest + offset;
	}
}
