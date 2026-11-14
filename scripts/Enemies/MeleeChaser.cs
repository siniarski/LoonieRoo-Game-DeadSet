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

	[ExportGroup("Gait")]
	/// <summary>Bob height while running, in metres. Keep it small on common enemies so heads stay level.</summary>
	[Export] public float BobHeight { get; set; } = 0.03f;
	/// <summary>Bobs per metre travelled.</summary>
	[Export] public float BobFrequency { get; set; } = 1.2f;
	/// <summary>Hops (bounces off the ground) rather than a smooth bob.</summary>
	[Export] public bool Hops { get; set; }
	/// <summary>Idle hover bob for flyers, in metres.</summary>
	[Export] public float HoverBob { get; set; } = 0.12f;

	private enum State { Chase, Windup, Recover }

	private MeleeAttack _melee;
	private Node3D _striker;
	private Vector3 _strikerRest;
	private Vector3 _visualRest;
	private State _state = State.Chase;
	private float _stateTimer;
	private float _bobPhase;

	public override void _Ready()
	{
		base._Ready();
		_melee = GetNode<MeleeAttack>("MeleeAttack");
		_melee.Damage = Stats.AttackDamage;
		_melee.Range = Stats.AttackRange;
		_melee.Cooldown = Stats.AttackCooldown;

		_striker = StrikerPath == null || StrikerPath.IsEmpty ? null : GetNodeOrNull<Node3D>(StrikerPath);
		if (_striker != null)
			_strikerRest = _striker.Position;
		_visualRest = Visual.Position;
		_bobPhase = GD.Randf() * Mathf.Tau;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (Health.IsDead)
			return;

		if (!HasLiveTarget)
		{
			_state = State.Chase;
			Steer(Vector3.Zero, dt);
			return;
		}

		Vector3 toTarget = Target.GlobalPosition - GlobalPosition;
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
				bool landed = !lunging || Flying || IsOnFloor();
				if (_stateTimer <= 0f && landed)
					_state = State.Chase;
				Steer(Vector3.Zero, dt, allowSteering: !lunging || IsOnFloor());
				return;
		}

		if (distance <= Stats.AttackRange && _melee.IsReady && (Flying || IsOnFloor()))
		{
			StartWindup(toTargetDir);
			Steer(Vector3.Zero, dt, allowSteering: !lunging);
			return;
		}

		// In reach but still cooling down: hold ground instead of crowding into the player's face.
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
				PlaceholderSfx.PlayAt(GetParent(), sound, GlobalPosition + Vector3.Up * EffectHeight, -6f);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		AnimateGait(dt);
		AnimateStriker();
	}

	private void AnimateGait(float dt)
	{
		float speed = new Vector2(Velocity.X, Velocity.Z).Length();
		Vector3 offset = Vector3.Zero;

		if (Flying)
		{
			_bobPhase += dt * 2.5f;
			offset.Y = Mathf.Sin(_bobPhase) * HoverBob;
		}
		else if (_state == State.Chase && IsOnFloor())
		{
			_bobPhase += speed * BobFrequency * Mathf.Pi * dt;
			float amount = Mathf.Min(speed / Mathf.Max(Stats.MoveSpeed, 0.01f), 1f);
			float wave = Hops ? Mathf.Abs(Mathf.Sin(_bobPhase)) : Mathf.Sin(_bobPhase) * 0.5f + 0.5f;
			offset.Y = wave * BobHeight * amount;
		}

		Visual.Position = _visualRest + offset;
	}

	private void AnimateStriker()
	{
		if (_striker == null)
			return;

		Vector3 offset = Vector3.Zero;
		if (_state == State.Windup)
		{
			float t = 1f - Mathf.Clamp(_stateTimer / Mathf.Max(WindupTime, 0.001f), 0f, 1f);
			offset = StrikerPullback * Mathf.SmoothStep(0f, 1f, t);
		}
		else if (_state == State.Recover)
		{
			// Snap out to the thrust, then ease back to rest over the recovery.
			float elapsed = RecoverTime - _stateTimer;
			const float jabTime = 0.05f;
			offset = elapsed < jabTime
				? StrikerPullback.Lerp(StrikerThrust, elapsed / jabTime)
				: StrikerThrust.Lerp(Vector3.Zero, Mathf.Clamp((elapsed - jabTime) / Mathf.Max(RecoverTime - jabTime, 0.01f), 0f, 1f));
		}

		_striker.Position = _strikerRest + offset;
	}
}
