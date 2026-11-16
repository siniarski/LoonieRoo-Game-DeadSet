using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Core;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// The variant half of Enemy: applying an EnemyVariant (stats, size, gear and armour plates),
/// ranged attacks, being hurried by a howl, packs, escorts and things left behind on death.
/// Behaviour scripts opt in to the parts that make sense for them.
/// </summary>
public abstract partial class Enemy
{
	/// <summary>The variant this enemy was spawned as, or null for the plain one.</summary>
	public EnemyVariant Variant { get; private set; }
	/// <summary>Armour plates still on it.</summary>
	public IReadOnlyList<ArmourPlate> Plates => _plates;
	/// <summary>The pack leader it moves with (dingo alpha), if any. Cleared when the leader dies.</summary>
	public Enemy Leader { get; set; }
	/// <summary>The enemy it escorts (a chick's mother), told when this one dies.</summary>
	public Enemy EscortOf { get; set; }
	public bool IsEnraged { get; private set; }
	public bool IsDead => Health == null || Health.IsDead;

	/// <summary>Speed set by the behaviour's mood (calm, enraged); 1 = normal.</summary>
	protected float SpeedFactor { get; set; } = 1f;
	/// <summary>Run speed right now: stats, mood and any howl hurry.</summary>
	protected float MoveSpeed => Stats.MoveSpeed * SpeedFactor * (_hurryTimer > 0f ? _hurryMultiplier : 1f);

	/// <summary>Howls that hurried at least one enemy this session (for the self-check).</summary>
	public static int Howls { get; set; }
	public static int Enrages { get; set; }
	public static int DeathSpawns { get; set; }

	private readonly List<ArmourPlate> _plates = new();
	private int _gearCount;
	private readonly List<Node3D> _enrageGear = new();
	private float _hurryTimer;
	private float _hurryMultiplier = 1f;
	private float _rangedTimer;
	private float _rangedWindup = -1f;
	private MeshInstance3D _tell;

	/// <summary>
	/// Turns this enemy into a variant. Call after instancing and before it enters the tree
	/// (and after MakeChunky, if used), so the hit reaction picks up the new size.
	/// </summary>
	public void ApplyVariant(EnemyVariant variant)
	{
		if (variant == null || Variant != null)
			return;
		Variant = variant;

		// Its own copy of the numbers, so the plain ones stay as they are.
		var stats = (EnemyStats)Stats.Duplicate();
		stats.MaxHealth *= variant.HealthMultiplier;
		stats.MoveSpeed *= variant.SpeedMultiplier;
		stats.AttackDamage *= variant.DamageMultiplier;
		stats.AttackCooldown *= variant.CooldownMultiplier;
		stats.Bounty = Mathf.Max(1, Mathf.RoundToInt(stats.Bounty * variant.BountyMultiplier));
		Stats = stats;

		var visual = GetNode<Node3D>("Visual");
		float size = variant.Size;
		visual.Scale *= new Vector3(size * variant.Width, size, size * variant.Width);
		if (!Mathf.IsEqualApprox(size, 1f))
		{
			foreach (Node child in GetChildren())
			{
				if (child is not CollisionShape3D shape)
					continue;
				shape.Position *= size;
				shape.Scale = Vector3.One * size;
			}
			EffectHeight *= size;
		}

		foreach (GearPiece piece in variant.Gear)
			if (piece != null)
				BuildGear(piece, visual);
	}

	private void BuildGear(GearPiece piece, Node3D visual)
	{
		Node3D parent = GetNodeOrNull<Node3D>(piece.Attach) ?? visual;
		Vector3 s = piece.Size;
		Mesh mesh = piece.Shape switch
		{
			GearPiece.ShapeKind.Cylinder => new CylinderMesh { TopRadius = s.X, BottomRadius = s.X, Height = s.Y, RadialSegments = 10, Rings = 1 },
			GearPiece.ShapeKind.Torus => new TorusMesh { OuterRadius = s.X, InnerRadius = Mathf.Max(s.X - s.Y, 0.01f), Rings = 12, RingSegments = 6 },
			GearPiece.ShapeKind.Sphere => new SphereMesh { Radius = s.X, Height = s.X * 2f, RadialSegments = 10, Rings = 5 },
			GearPiece.ShapeKind.Prism => new PrismMesh { Size = s },
			_ => new BoxMesh { Size = s },
		};
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = piece.Color,
			ShadingMode = piece.Glow ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
			Roughness = 0.7f,
			Metallic = piece.Heavy ? 0.4f : 0f,
		});
		var node = new MeshInstance3D { Name = $"Gear{_gearCount++}", Mesh = mesh, Position = piece.Offset, RotationDegrees = piece.RotationDegrees };
		parent.AddChild(node);
		if (piece.ShowWhenEnraged)
		{
			node.Visible = false;
			_enrageGear.Add(node);
		}
		if (piece.Armour <= 0f)
			return;

		Shape3D shape = piece.Shape switch
		{
			GearPiece.ShapeKind.Cylinder => new CylinderShape3D { Radius = s.X, Height = s.Y },
			GearPiece.ShapeKind.Torus => new CylinderShape3D { Radius = s.X, Height = s.Y * 2f },
			GearPiece.ShapeKind.Sphere => new SphereShape3D { Radius = s.X },
			_ => new BoxShape3D { Size = s },
		};
		ArmourPlate plate = ArmourPlate.Attach(node, shape, this, piece.Armour, piece.Heavy, piece.CoversHead);
		_plates.Add(plate);
		plate.Broken += p => _plates.Remove(p);
	}

	/// <summary>Turns its plates' collision on or off (off while burrowed).</summary>
	protected void SetPlatesSolid(bool solid)
	{
		foreach (ArmourPlate plate in _plates)
			if (IsInstanceValid(plate))
				plate.CollisionLayer = solid ? 4u : 0u;
	}

	/// <summary>An intact plate covering the head, if any (melee head hits land on it).</summary>
	public ArmourPlate HeadPlate()
	{
		foreach (ArmourPlate plate in _plates)
			if (IsInstanceValid(plate) && plate.CoversHead && !plate.IsBroken)
				return plate;
		return null;
	}

	/// <summary>Melee shreds light gear (ties, wraps, wire collars); heavy plates shrug it off.</summary>
	public void ShredLightGear(float damage)
	{
		foreach (ArmourPlate plate in _plates.ToArray())
			if (IsInstanceValid(plate) && !plate.Heavy && !plate.IsBroken)
				plate.TakeHit(damage, plate.GlobalPosition, Vector3.Up);
	}

	/// <summary>Speeds it up for a while (a howler's call).</summary>
	public void Hurry(float multiplier, float seconds)
	{
		_hurryMultiplier = Mathf.Max(multiplier, 1f);
		_hurryTimer = Mathf.Max(_hurryTimer, seconds);
	}

	/// <summary>Goes berserk: faster, more hits, and its rage gear shows. Behaviours add the details.</summary>
	public virtual void Enrage()
	{
		if (IsEnraged || IsDead)
			return;
		IsEnraged = true;
		Enrages++;
		foreach (Node3D gear in _enrageGear)
			if (IsInstanceValid(gear))
				gear.Visible = true;
		if (DeathCue != EnemyCue.None)
			PlaceholderSfx.PlayAt(GetParent(), PlaceholderSfx.ForCue(DeathCue), GlobalPosition + Vector3.Up * EffectHeight, 4f, 0.05f);
		ImpactEffects.SpawnSparks(GetParent(), GlobalPosition + Vector3.Up * EffectHeight, Vector3.Up, new Color(1f, 0.25f, 0.2f), 20, 1.5f, 0.4f);
	}

	/// <summary>Called on a mother when one of her escorts dies.</summary>
	public void OnEscortDied()
	{
		if (Variant is { EnrageWhenEscortDies: true })
			Enrage();
	}

	/// <summary>Runs every physics tick from BeginTick.</summary>
	private void TickVariant(float dt)
	{
		if (_hurryTimer > 0f)
			_hurryTimer -= dt;
	}

	private void ReadyVariant()
	{
		if (Variant?.Ranged is { } ranged)
			_rangedTimer = ranged.FirstDelay * (float)GD.RandRange(0.7, 1.3);
		if (Variant is { EscortCount: > 0, Escort: not null })
			CallDeferred(MethodName.SpawnEscorts);
	}

	private void SpawnEscorts()
	{
		for (int i = 0; i < Variant.EscortCount; i++)
		{
			float angle = Mathf.Tau * i / Variant.EscortCount;
			Enemy chick = SpawnKin(Variant.Escort, GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.4f);
			if (chick != null)
				chick.EscortOf = this;
		}
	}

	/// <summary>Spawns another of this enemy scene as the given variant and tells the wave about it.</summary>
	protected Enemy SpawnKin(EnemyVariant variant, Vector3 at)
	{
		if (string.IsNullOrEmpty(SceneFilePath) || GetParent() == null)
			return null;
		var scene = GD.Load<PackedScene>(SceneFilePath);
		if (scene?.Instantiate() is not Enemy kin)
			return null;
		kin.ApplyVariant(variant);
		Vector3 ground = NavGrid.Built ? NavGrid.SnapToGround(at) : new Vector3(at.X, GlobalPosition.Y, at.Z);
		kin.Position = ground + Vector3.Up * 0.1f;
		GetParent().AddChild(kin);
		kin.GlobalPosition = ground + Vector3.Up * 0.1f;
		GameEvents.Instance?.EmitSignal(GameEvents.SignalName.EnemySpawned, kin);
		return kin;
	}

	/// <summary>Variant business when it dies: plates go with it, splashes, things left behind.</summary>
	private void DieVariant(Node parent)
	{
		RangedSlots.Release(this);
		foreach (ArmourPlate plate in _plates)
			if (IsInstanceValid(plate))
				plate.QueueFree();
		_plates.Clear();

		if (IsInstanceValid(EscortOf) && !EscortOf.IsDead)
			EscortOf.OnEscortDied();
		if (Variant == null)
			return;

		if (Health.LastHitWeakSpot && Variant.HeadPopSplashRadius > 0f)
		{
			float r2 = Variant.HeadPopSplashRadius * Variant.HeadPopSplashRadius;
			Vector3 at = GlobalPosition;
			foreach (Enemy other in All.ToArray())
			{
				if (other == this || other.IsDead || other.GlobalPosition.DistanceSquaredTo(at) > r2)
					continue;
				other.Health.LastHitWeakSpot = false;
				other.Health.ApplyDamage(Variant.HeadPopSplashDamage);
			}
			ImpactEffects.SpawnSparks(parent, at + Vector3.Up * EffectHeight, Vector3.Up, DeathSparkColor, 40, 2.5f, 0.6f, 1.3f);
		}

		if (Variant.SpawnOnDeath != null)
		{
			for (int i = 0; i < Variant.SpawnOnDeathCount; i++)
			{
				if (SpawnKin(Variant.SpawnOnDeath, GlobalPosition + new Vector3((float)GD.RandRange(-0.5, 0.5), 0f, (float)GD.RandRange(-0.5, 0.5))) != null)
					DeathSpawns++;
			}
		}
	}

	// ---- Ranged -----------------------------------------------------------------------------

	/// <summary>
	/// Runs the variant's ranged attack, if it has one. Call from the behaviour's chase state.
	/// Returns true while it's winding up a shot it has to stand still for: hold position then.
	/// </summary>
	protected bool TickRanged(float dt, float distance)
	{
		RangedAttack r = Variant?.Ranged;
		if (r == null)
			return false;
		bool stops = r.Kind != RangedAttack.AttackKind.Drop;

		if (_rangedWindup >= 0f)
		{
			_rangedWindup -= dt;
			float k = 1f - Mathf.Clamp(_rangedWindup / Mathf.Max(r.Windup, 0.01f), 0f, 1f);
			if (_tell != null)
				_tell.Scale = Vector3.One * Mathf.Lerp(0.3f, 1.2f, k) * (1f + 0.1f * Mathf.Sin(k * 40f));
			if (_rangedWindup <= 0f)
			{
				_rangedWindup = -1f;
				if (_tell != null)
					_tell.Visible = false;
				if (HasLiveTarget)
					FireRanged(r);
				RangedSlots.Release(this);
				_rangedTimer = r.Cooldown * (float)GD.RandRange(0.8, 1.2);
			}
			return stops;
		}

		_rangedTimer -= dt;
		if (_rangedTimer > 0f || !HasLiveTarget || distance < r.MinRange || distance > r.MaxRange)
			return false;
		if (r.Kind is RangedAttack.AttackKind.Lob or RangedAttack.AttackKind.Spread && !HasClearShot())
		{
			_rangedTimer = 0.4f;
			return false;
		}
		if (!RangedSlots.TryAcquire(this))
		{
			_rangedTimer = 0.5f;
			return false;
		}
		_rangedWindup = r.Windup;
		ShowTell(r);
		AudioStream cue = PlaceholderSfx.ForCue(r.Cue);
		if (cue != null)
			PlaceholderSfx.PlayAt(GetParent(), cue, LaunchPoint(r), r.Kind == RangedAttack.AttackKind.Howl ? 4f : 0f, 0.06f);
		return stops;
	}

	/// <summary>True while it's winding up a ranged shot.</summary>
	protected bool RangedWindingUp => _rangedWindup >= 0f;

	private Vector3 LaunchPoint(RangedAttack r) => Visual.GlobalTransform * r.LaunchOffset;

	private bool HasClearShot()
	{
		Vector3 from = BodyPosition + Vector3.Up * Mathf.Max(EffectHeight, 0.8f);
		Vector3 to = TargetPosition + Vector3.Up * 1.2f;
		var query = PhysicsRayQueryParameters3D.Create(from, to, 1);
		return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	private void ShowTell(RangedAttack r)
	{
		if (_tell == null)
		{
			var mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 10, Rings = 5 };
			mesh.SurfaceSetMaterial(0, new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(r.Color, 0.85f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			});
			_tell = new MeshInstance3D { Name = "RangedTell", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
			Visual.AddChild(_tell);
			Vector3 scale = Visual.Scale;
			_tell.Position = r.LaunchOffset;
			// The tell's size is set in world terms (ProjectileSize), whatever the model's scale.
			_tellSize = r.Kind == RangedAttack.AttackKind.Howl ? 0.45f : r.ProjectileSize * 1.3f / Mathf.Max(scale.X, 0.01f);
		}
		_tell.Visible = true;
		_tell.Scale = Vector3.One * 0.3f;
		((SphereMesh)_tell.Mesh).Radius = _tellSize * 0.5f;
		((SphereMesh)_tell.Mesh).Height = _tellSize;
	}

	private float _tellSize = 0.4f;

	private void FireRanged(RangedAttack r)
	{
		Node parent = GetParent();
		Vector3 from = LaunchPoint(r);
		Vector3 feet = TargetPosition;
		Vector3 lead = Target is CharacterBody3D body ? new Vector3(body.Velocity.X, 0f, body.Velocity.Z) : Vector3.Zero;
		switch (r.Kind)
		{
			case RangedAttack.AttackKind.Lob:
			case RangedAttack.AttackKind.Drop:
			{
				// Lead a moving player a little, so standing still is the worst choice but moving always works.
				float leadShare = r.Kind == RangedAttack.AttackKind.Drop ? 0.3f : 0.5f;
				Vector3 land = feet + lead * r.FlightTime * leadShare;
				land.Y = feet.Y;
				Lobbed.Throw(parent, from, land, r.FlightTime, r, GlobalPosition);
				PlaceholderSfx.PlayAt(parent, PlaceholderSfx.Launch, from, -2f, 0.1f);
				break;
			}
			case RangedAttack.AttackKind.Spread:
			{
				Vector3 to = feet - BodyPosition;
				to.Y = 0f;
				float distance = to.Length();
				for (int i = 0; i < Mathf.Max(r.Count, 1); i++)
				{
					float t = r.Count > 1 ? i / (float)(r.Count - 1) - 0.5f : 0f;
					float angle = Mathf.DegToRad(r.SpreadDegrees) * t * 2f + (float)GD.RandRange(-0.05, 0.05);
					Vector3 dir = to.Normalized().Rotated(Vector3.Up, angle);
					Vector3 land = BodyPosition + dir * (distance + (float)GD.RandRange(-0.8, 0.8));
					land.Y = feet.Y;
					Lobbed.Throw(parent, from, land, r.FlightTime * (float)GD.RandRange(0.9, 1.1), r, GlobalPosition);
				}
				PlaceholderSfx.PlayAt(parent, PlaceholderSfx.Launch, from, -4f, 0.2f);
				break;
			}
			case RangedAttack.AttackKind.Howl:
			{
				int hurried = 0;
				float r2 = r.HowlRadius * r.HowlRadius;
				foreach (Enemy other in All)
				{
					if (other == this || other.IsDead || other.GlobalPosition.DistanceSquaredTo(GlobalPosition) > r2)
						continue;
					other.Hurry(r.HowlSpeedMultiplier, r.HowlSeconds);
					hurried++;
				}
				if (hurried > 0)
					Howls++;
				ImpactEffects.SpawnSparks(parent, from, Vector3.Up, r.Color, 20, 1.5f, 0.5f);
				break;
			}
		}
	}
}

/// <summary>Caps how many enemies can be winding up or firing a ranged attack at once.</summary>
public static class RangedSlots
{
	public static int Max { get; set; } = 2;
	private static readonly HashSet<Enemy> Holders = new();

	public static bool TryAcquire(Enemy enemy)
	{
		Holders.RemoveWhere(e => !GodotObject.IsInstanceValid(e));
		if (Holders.Contains(enemy))
			return true;
		if (Holders.Count >= Max)
			return false;
		Holders.Add(enemy);
		return true;
	}

	public static void Release(Enemy enemy) => Holders.Remove(enemy);
}
