using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// A flyer that swoops like a nesting magpie: it circles the player overhead, calls out, then
/// dives at their head in a near-straight line and climbs back out. The dive barely homes, so a
/// sidestep or a step back makes it miss, and it's the one moment it comes down to eye level.
/// </summary>
public partial class Swooper : Enemy
{
	[ExportGroup("Circling")]
	/// <summary>Radius it circles the player at, in metres.</summary>
	[Export] public float CircleRadius { get; set; } = 7f;
	[Export] public float CircleSpeed { get; set; } = 6f;
	/// <summary>Seconds of circling between dives (random in this range).</summary>
	[Export] public float CircleTimeMin { get; set; } = 1.2f;
	[Export] public float CircleTimeMax { get; set; } = 2.6f;

	[ExportGroup("Dive")]
	/// <summary>Seconds it hangs and calls out before diving. This is the player's warning.</summary>
	[Export] public float WindupTime { get; set; } = 0.55f;
	[Export] public float DiveSpeed { get; set; } = 13f;
	/// <summary>How fast the dive can bend towards a moving player, in radians per second.</summary>
	[Export] public float DiveTurnRate { get; set; } = 0.9f;
	[Export] public float DiveMaxTime { get; set; } = 1.4f;
	/// <summary>The dive hits when its head passes within this many metres of the player's head.</summary>
	[Export] public float HitRadius { get; set; } = 0.9f;
	/// <summary>Where it aims, in metres above the player's feet.</summary>
	[Export] public float AimHeight { get; set; } = 1.55f;
	/// <summary>Seconds it climbs away after a dive before it can circle again.</summary>
	[Export] public float PullOutTime { get; set; } = 0.8f;
	[Export] public float ClimbSpeed { get; set; } = 4.5f;
	/// <summary>Nose-down tilt during the dive, in radians.</summary>
	[Export] public float DivePitch { get; set; } = 0.55f;
	/// <summary>Sideways shove given to the player when the dive connects, in m/s.</summary>
	[Export] public float HitKnockback { get; set; } = 3f;
	[Export] public EnemyCue WarnCue { get; set; } = EnemyCue.Warble;
	[Export] public float WarnVolumeDb { get; set; } = 2f;
	[Export] public EnemyCue AttackCue { get; set; } = EnemyCue.Peck;
	[Export] public float HoverBob { get; set; } = 0.15f;

	public enum State { Approach, Circle, Windup, Dive, PullOut }
	public State Current => _state;

	/// <summary>Dives that hit the player this session (for the self-check).</summary>
	public static int DiveHits { get; set; }
	/// <summary>Dives started this session (for the self-check).</summary>
	public static int Dives { get; set; }

	private MeleeAttack _melee;
	private State _state = State.Approach;
	private float _timer;
	private float _circleSign = 1f;
	private Vector3 _diveDir;
	private float _pitch;
	private float _bob;
	private Node3D _striker;
	private Vector3 _strikerRest;
	private Vector3 _visualRest;

	public override void _Ready()
	{
		base._Ready();
		_melee = GetNode<MeleeAttack>("MeleeAttack");
		_melee.Damage = Stats.AttackDamage;
		_melee.Range = Stats.AttackRange;
		_melee.Cooldown = Stats.AttackCooldown;
		_striker = GetNodeOrNull<Node3D>("Visual/Striker");
		if (_striker != null)
			_strikerRest = _striker.Position;
		_visualRest = Visual.Position;
		_circleSign = GD.Randf() < 0.5f ? -1f : 1f;
		_bob = GD.Randf() * Mathf.Tau;
	}

	/// <summary>A hammer hit knocks it out of a wind-up.</summary>
	public override void Shove(Vector3 push)
	{
		base.Shove(push);
		if (_state == State.Windup)
			StartCircling();
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

	private Vector3 HeadPoint => BodyPosition + Vector3.Up * EffectHeight;
	private Vector3 AimPoint => TargetPosition + Vector3.Up * AimHeight;

	private void Tick(float dt)
	{
		if (!HasLiveTarget)
		{
			if (_state is State.Windup or State.Dive)
				_state = State.PullOut;
			AttackSlots.Release(this);
		}

		Vector3 toTarget = TargetPosition - BodyPosition;
		toTarget.Y = 0f;
		float distance = toTarget.Length();
		Vector3 toTargetDir = toTarget / Mathf.Max(distance, 0.001f);

		switch (_state)
		{
			case State.Approach:
			{
				if (HasLiveTarget && distance <= CircleRadius * 1.3f)
				{
					StartCircling();
					break;
				}
				Vector3 direction = HasLiveTarget ? ChaseDirection(dt) : Vector3.Zero;
				Steer(direction * Stats.MoveSpeed + Separation(), dt);
				FaceDirection(direction, dt);
				break;
			}

			case State.Circle:
			{
				if (distance > CircleRadius * 1.8f)
				{
					_state = State.Approach;
					break;
				}
				Vector3 tangent = Vector3.Up.Cross(toTargetDir) * _circleSign;
				Vector3 desired = tangent * CircleSpeed + toTargetDir * Mathf.Clamp(distance - CircleRadius, -3f, 3f) * 1.5f;
				Steer(desired + Separation(), dt);
				FaceDirection(desired, dt);
				_timer -= dt;
				if (_timer <= 0f && HasLiveTarget && _melee.IsReady && AttackSlots.TryAcquire(this))
				{
					_state = State.Windup;
					_timer = WindupTime;
					AudioStream call = PlaceholderSfx.ForCue(WarnCue);
					if (call != null)
						PlaceholderSfx.PlayAt(GetParent(), call, HeadPoint, WarnVolumeDb, 0.08f);
				}
				break;
			}

			case State.Windup:
				Steer(Vector3.Zero, dt);
				FaceDirection(toTargetDir, dt, 12f);
				_timer -= dt;
				if (_timer <= 0f)
				{
					_state = State.Dive;
					_timer = DiveMaxTime;
					_diveDir = (AimPoint - HeadPoint).Normalized();
					_melee.StartCooldown();
					Dives++;
				}
				break;

			case State.Dive:
			{
				Vector3 want = AimPoint - HeadPoint;
				// Bend towards the player a little: enough to punish standing still, not a sidestep.
				float angle = _diveDir.AngleTo(want);
				if (angle > 0.0001f)
				{
					float turn = Mathf.Min(angle, DiveTurnRate * dt);
					Vector3 axis = _diveDir.Cross(want);
					if (axis.LengthSquared() > 1e-8f)
						_diveDir = _diveDir.Rotated(axis.Normalized(), turn).Normalized();
				}
				Velocity = _diveDir * DiveSpeed;
				Move();
				FaceDirection(new Vector3(_diveDir.X, 0f, _diveDir.Z), dt, 20f);
				_timer -= dt;

				Vector3 head = GlobalPosition + Vector3.Up * EffectHeight;
				if (HasLiveTarget && (head.DistanceTo(AimPoint) <= HitRadius || TouchedTarget()))
				{
					HitTarget();
					StartPullOut();
				}
				else if (_timer <= 0f || _diveDir.Dot(AimPoint - head) < 0f || head.Y < 0.6f || GetSlideCollisionCount() > 0)
				{
					StartPullOut();   // missed: it went past, or ran into something
				}
				break;
			}

			case State.PullOut:
			{
				Vector3 flat = new Vector3(_diveDir.X, 0f, _diveDir.Z) * DiveSpeed * 0.55f;
				float climb = GlobalPosition.Y < 0f ? ClimbSpeed : 0f;
				Velocity = new Vector3(flat.X, climb, flat.Z);
				Move();
				_timer -= dt;
				if (_timer <= 0f && GlobalPosition.Y >= -0.05f)
				{
					Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
					StartCircling();
				}
				break;
			}
		}
	}

	/// <summary>True when this tick's move ran into the player (the body reaches them before the beak point does).</summary>
	private bool TouchedTarget()
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
			if (GetSlideCollision(i).GetCollider() == Target)
				return true;
		return false;
	}

	private void StartCircling()
	{
		_state = State.Circle;
		_timer = (float)GD.RandRange(CircleTimeMin, CircleTimeMax);
		AttackSlots.Release(this);
	}

	private void StartPullOut()
	{
		_state = State.PullOut;
		_timer = PullOutTime;
		AttackSlots.Release(this);
	}

	private void HitTarget()
	{
		if (!_melee.ApplyTo(TargetHealth))
			return;
		DiveHits++;
		AudioStream sound = PlaceholderSfx.ForCue(AttackCue);
		if (sound != null)
			PlaceholderSfx.PlayAt(GetParent(), sound, AimPoint, -2f);
		if (Target is CharacterBody3D body && HitKnockback > 0f)
		{
			Vector3 push = new Vector3(_diveDir.X, 0f, _diveDir.Z).Normalized() * HitKnockback;
			body.Velocity += push;
		}
	}

	/// <summary>A local offset turned to the model's current heading.</summary>
	private Vector3 RotateYaw(Vector3 local) => local.Rotated(Vector3.Up, Visual.Rotation.Y);

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		float pitchTarget = _state switch
		{
			State.Windup => 0.35f,
			State.Dive => -DivePitch,
			State.PullOut => 0.4f,
			_ => 0f,
		};
		_pitch = Mathf.Lerp(_pitch, pitchTarget, 1f - Mathf.Exp(-12f * dt));
		Vector3 rotation = Visual.Rotation;
		rotation.X = _pitch;
		Visual.Rotation = rotation;

		_bob += dt * (_state == State.Windup ? 14f : 2.5f);
		float bob = _state == State.Dive ? 0f : Mathf.Sin(_bob) * HoverBob * (_state == State.Windup ? 0.5f : 1f);
		// The model pivots at its feet (on the ground), but the bird flies EffectHeight up: shift
		// it so the tilt turns the bird about its own body instead of swinging it through the air.
		float h = EffectHeight;
		Vector3 pivotFix = new(0f, h - h * Mathf.Cos(_pitch), -h * Mathf.Sin(_pitch));
		Visual.Position = _visualRest + new Vector3(0f, bob, 0f) + RotateYaw(pivotFix);

		if (_striker != null)
		{
			Vector3 offset = _state switch
			{
				State.Windup => new Vector3(0f, 0.1f, 0.15f),
				State.Dive => new Vector3(0f, -0.1f, -0.3f),
				_ => Vector3.Zero,
			};
			_striker.Position = _striker.Position.Lerp(_strikerRest + offset, 1f - Mathf.Exp(-20f * dt));
		}
	}
}
