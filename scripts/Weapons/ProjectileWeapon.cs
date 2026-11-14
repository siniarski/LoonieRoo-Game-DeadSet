using Deadset.Effects;
using Godot;

namespace Deadset.Weapons;

/// <summary>
/// A weapon that fires physical projectiles (the Nail Gun).
/// Also owns its own look and sound: model kick, reload dip, muzzle flash and SFX.
/// </summary>
public partial class ProjectileWeapon : Weapon
{
	[Export] public PackedScene ProjectileScene { get; set; }

	[ExportGroup("Feel")]
	[Export] public float MuzzleFlashTime { get; set; } = 0.04f;
	/// <summary>How fast the gun model settles after a shot. Higher = snappier.</summary>
	[Export] public float KickRecoverySpeed { get; set; } = 14f;
	[Export] public float ReloadDipDistance { get; set; } = 0.12f;
	[Export] public float ReloadTiltDegrees { get; set; } = 30f;

	[ExportGroup("Aiming pose")]
	/// <summary>Model kick multiplier while fully aimed, so the sights stay readable.</summary>
	[Export(PropertyHint.Range, "0,1")] public float AdsKickMultiplier { get; set; } = 0.5f;
	/// <summary>Pulls the gun toward (+) or away from (-) the eye when aimed, in metres.</summary>
	[Export] public float AdsDepthOffset { get; set; } = 0.02f;

	[ExportGroup("Volume (dB)")]
	[Export] public float FireVolumeDb { get; set; } = -4f;
	[Export] public float HitVolumeDb { get; set; } = -8f;
	[Export] public float ReloadVolumeDb { get; set; } = -6f;

	private Node3D _model;
	private Node3D _muzzle;
	private Node3D _flash;
	private Vector3 _modelRestPosition;
	private Vector3 _modelRestRotation;
	private Vector3 _hipPosition;
	private Vector3 _adsOffset;
	private float _aimVisual;
	private float _kick;
	private float _reloadBlend;
	private float _flashTimer;

	private AudioStreamPlayer _fireSfx;
	private AudioStreamPlayer _hitSfx;
	private AudioStreamPlayer _handlingSfx;

	public override void _Ready()
	{
		base._Ready();

		_model = GetNode<Node3D>("Model");
		_muzzle = GetNode<Node3D>("Model/Muzzle");
		_flash = GetNode<Node3D>("Model/Muzzle/Flash");
		_flash.Visible = false;
		_modelRestPosition = _model.Position;
		_modelRestRotation = _model.RotationDegrees;

		// Work out how far to slide the whole gun so the sight line sits dead centre on screen.
		// This node's parent is the camera, so Transform maps into camera space.
		_hipPosition = Position;
		var aimPoint = GetNode<Node3D>("Model/AimPoint");
		Vector3 sightInCamera = Transform * (_model.Transform * aimPoint.Position);
		_adsOffset = new Vector3(-sightInCamera.X, -sightInCamera.Y, AdsDepthOffset);

		_fireSfx = MakePlayer(FireVolumeDb, 4);
		_fireSfx.Stream = PlaceholderSfx.NailShot;
		_hitSfx = MakePlayer(HitVolumeDb, 3);
		_handlingSfx = MakePlayer(ReloadVolumeDb, 2);

		ReloadStarted += _ => PlayHandling(PlaceholderSfx.ReloadStart);
		ReloadFinished += () => PlayHandling(PlaceholderSfx.ReloadEnd);
		DryFired += () => PlayHandling(PlaceholderSfx.DryFire);
		HitLanded += OnHitLanded;
	}

	protected override void FireShot()
	{
		Transform3D aim = AimSource.GlobalTransform;
		Vector3 direction = ApplySpread(-aim.Basis.Z, aim.Basis, CurrentSpreadDegrees);

		var projectile = ProjectileScene.Instantiate<Nail>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.Launch(new Nail.LaunchParams
		{
			Origin = aim.Origin,
			Direction = direction,
			Speed = Stats.ProjectileSpeed,
			Damage = Stats.Damage,
			MaxRange = Stats.MaxRange,
			VisualStart = _muzzle.GlobalPosition,
			Source = this,
			Exclude = Wielder?.GetRid() ?? new Rid(),
		});

		_kick = Mathf.Min(_kick + 1f, 1.6f);
		_flashTimer = MuzzleFlashTime;
		_flash.Visible = true;
		_flash.Rotation = new Vector3(0f, 0f, (float)GD.RandRange(0, Mathf.Tau));
		_flash.Scale = Vector3.One * (float)GD.RandRange(0.8, 1.25);

		_fireSfx.PitchScale = (float)GD.RandRange(0.92, 1.08);
		_fireSfx.Play();
	}

	private static Vector3 ApplySpread(Vector3 forward, Basis basis, float spreadDegrees)
	{
		if (spreadDegrees <= 0f)
			return forward;

		// Uniform random point inside a cone around forward.
		float angle = Mathf.DegToRad(spreadDegrees) * Mathf.Sqrt(GD.Randf());
		float roll = GD.Randf() * Mathf.Tau;
		Vector3 axis = (basis.X * Mathf.Cos(roll) + basis.Y * Mathf.Sin(roll)).Normalized();
		return forward.Rotated(axis, angle);
	}

	private void OnHitLanded(bool killed, bool weakSpot)
	{
		_hitSfx.Stream = killed ? PlaceholderSfx.KillTick : PlaceholderSfx.HitTick;
		_hitSfx.Play();
		if (weakSpot)
			PlaceholderSfx.PlayAt(this, PlaceholderSfx.HeadshotTink, GlobalPosition, -4f, 0.04f);
	}

	private void PlayHandling(AudioStream stream)
	{
		_handlingSfx.Stream = stream;
		_handlingSfx.Play();
	}

	private AudioStreamPlayer MakePlayer(float volumeDb, int polyphony)
	{
		var player = new AudioStreamPlayer { VolumeDb = volumeDb, MaxPolyphony = polyphony };
		AddChild(player);
		return player;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		_kick *= Mathf.Exp(-KickRecoverySpeed * dt);
		float reloadTarget = IsReloading ? 1f : 0f;
		_reloadBlend = Mathf.Lerp(_reloadBlend, reloadTarget, 1f - Mathf.Exp(-12f * dt));

		// AimBlend steps at physics rate; smooth it again for display, then ease in/out.
		_aimVisual = Mathf.Lerp(_aimVisual, AimBlend, 1f - Mathf.Exp(-30f * dt));
		float aim = Mathf.SmoothStep(0f, 1f, _aimVisual);
		Position = _hipPosition + _adsOffset * aim;
		float kick = _kick * Mathf.Lerp(1f, AdsKickMultiplier, aim);

		_model.Position = _modelRestPosition + new Vector3(
			0f,
			-ReloadDipDistance * _reloadBlend,
			Stats.ModelKickDistance * kick);
		_model.RotationDegrees = _modelRestRotation + new Vector3(
			Stats.ModelKickDegrees * kick - 10f * _reloadBlend,
			0f,
			ReloadTiltDegrees * _reloadBlend);

		if (_flashTimer > 0f)
		{
			_flashTimer -= dt;
			if (_flashTimer <= 0f)
				_flash.Visible = false;
		}
	}
}
