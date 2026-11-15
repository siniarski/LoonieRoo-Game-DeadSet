using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// The Drop Bear: a climber that ignores paths and comes straight at you. Anything in the way
/// (containers, the shed, besser walls) it climbs up and over. From the top of cover, if you're
/// close and below it, it crouches with a growl, then pounces down on you.
/// Prototype special: only spawned with --spawn=dropbear, not in the waves.
/// </summary>
public partial class DropBearClimber : Enemy
{
	private enum State { Run, Crouch, Pounce, Recover }

	[ExportGroup("Climbing")]
	/// <summary>Metres per second up a wall.</summary>
	[Export] public float ClimbSpeed { get; set; } = 3.2f;
	/// <summary>Hop given when it reaches the top of what it was climbing, so it lands on top.</summary>
	[Export] public float TopHopSpeed { get; set; } = 2.5f;
	/// <summary>How far it leans back while climbing, in degrees.</summary>
	[Export] public float ClimbLeanDegrees { get; set; } = 70f;

	[ExportGroup("Pounce")]
	/// <summary>Pounces only from at least this far above the target's feet.</summary>
	[Export] public float PounceMinHeight { get; set; } = 1.2f;
	/// <summary>Pounces when the target is within this horizontal distance.</summary>
	[Export] public float PounceRange { get; set; } = 8f;
	/// <summary>Seconds of crouch and growl before it leaps: the tell.</summary>
	[Export] public float CrouchTime { get; set; } = 0.45f;
	[Export] public float PounceFlightTime { get; set; } = 0.75f;
	[Export] public float PounceDamage { get; set; } = 15f;
	/// <summary>Seconds dazed after landing a pounce, an opening to shoot it.</summary>
	[Export] public float RecoverTime { get; set; } = 0.9f;

	private State _state = State.Run;
	private float _stateTimer;
	private bool _wasClimbing;
	private bool _pounceHit;
	private float _attackCooldown;
	private float _lean;
	private bool _crouchShown;
	private MeleeAttack _melee;

	public override void _Ready()
	{
		base._Ready();
		_melee = GetNode<MeleeAttack>("MeleeAttack");
		_melee.Damage = Stats.AttackDamage;
		_melee.Cooldown = Stats.AttackCooldown;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		BeginTick();

		Vector3 toTarget = TargetPosition - BodyPosition;
		Vector3 flat = new(toTarget.X, 0f, toTarget.Z);
		float distance = flat.Length();
		Vector3 dir = distance > 0.01f ? flat / distance : Vector3.Zero;
		Vector3 velocity = Velocity;
		bool climbing = false;
		_attackCooldown -= dt;
		_stateTimer -= dt;

		switch (_state)
		{
			case State.Run:
			{
				if (!HasLiveTarget)
				{
					velocity.X = velocity.Z = 0f;
					velocity.Y -= Gravity * dt;
					break;
				}
				// Up on cover, close, and above them: get ready to pounce.
				if (IsOnFloor() && -toTarget.Y >= PounceMinHeight && distance <= PounceRange)
				{
					SetState(State.Crouch, CrouchTime);
					PlaceholderSfx.PlayAt(GetParent(), PlaceholderSfx.Growl, GlobalPosition, -2f, 0.1f);
					velocity = Vector3.Zero;
					break;
				}

				// Pressed against something between us and the target: climb it.
				climbing = IsOnWall() && GetWallNormal().Dot(dir) < -0.4f;
				Vector3 run = (dir + Separation() * 0.3f) * Stats.MoveSpeed;
				if (climbing)
				{
					velocity = dir * 1.5f + Vector3.Up * ClimbSpeed;
				}
				else
				{
					velocity.X = run.X;
					velocity.Z = run.Z;
					velocity.Y = _wasClimbing ? TopHopSpeed : (IsOnFloor() ? 0f : velocity.Y - Gravity * dt);
				}

				if (distance <= Stats.AttackRange && IsOnFloor() && Mathf.Abs(toTarget.Y) < 1f && _attackCooldown <= 0f)
				{
					_attackCooldown = Stats.AttackCooldown;
					if (_melee.ApplyTo(TargetHealth))
						PlaceholderSfx.PlayAt(GetParent(), PlaceholderSfx.Thump, GlobalPosition, -6f);
				}
				break;
			}

			case State.Crouch:
				velocity = new Vector3(0f, IsOnFloor() ? 0f : velocity.Y - Gravity * dt, 0f);
				if (_stateTimer <= 0f)
				{
					// Ballistic leap that lands on the target's chest.
					float t = Mathf.Max(PounceFlightTime, 0.2f);
					float rise = TargetPosition.Y + 0.8f - BodyPosition.Y;
					velocity = flat / t + Vector3.Up * ((rise + 0.5f * Gravity * t * t) / t);
					_pounceHit = false;
					SetState(State.Pounce, 3f);
				}
				break;

			case State.Pounce:
				velocity.Y -= Gravity * dt;
				if (!_pounceHit && HasLiveTarget && GlobalPosition.DistanceTo(TargetPosition + Vector3.Up * 0.6f) < 1.4f)
				{
					_pounceHit = true;
					TargetHealth.ApplyDamage(PounceDamage, GlobalPosition);
					PlaceholderSfx.PlayAt(GetParent(), PlaceholderSfx.Thump, GlobalPosition, 0f);
					velocity = new Vector3(-dir.X * 2f, 1.5f, -dir.Z * 2f);   // bounce off
				}
				if ((IsOnFloor() && _stateTimer < 2.9f) || _stateTimer <= 0f)
					SetState(State.Recover, RecoverTime);
				break;

			case State.Recover:
				velocity = new Vector3(0f, IsOnFloor() ? 0f : velocity.Y - Gravity * dt, 0f);
				if (_stateTimer <= 0f)
					SetState(State.Run, 0f);
				break;
		}

		_wasClimbing = climbing;
		Velocity = velocity;
		MoveAndSlide();

		if (dir != Vector3.Zero)
			FaceDirection(dir, dt, 10f);
		// Lean back to face up the wall; hunch while crouching; stretch out mid-pounce.
		float leanTarget = climbing ? ClimbLeanDegrees : _state switch
		{
			State.Crouch => -18f,
			State.Pounce => 25f,
			_ => 0f,
		};
		_lean = Mathf.Lerp(_lean, leanTarget, 1f - Mathf.Exp(-14f * dt));
		Vector3 rotation = Visual.Rotation;
		rotation.X = Mathf.DegToRad(_lean);
		Visual.Rotation = rotation;
		bool crouching = _state == State.Crouch;
		if (crouching != _crouchShown)
		{
			_crouchShown = crouching;
			Visual.Scale = crouching ? new Vector3(1.1f, 0.85f, 1.1f) : Vector3.One;
		}

		EndTick();
	}

	private void SetState(State state, float time)
	{
		_state = state;
		_stateTimer = time;
	}
}
