using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Effects;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// A slow thrown blob (a mortar wombat's cube, an emu's spit, a magpie's stone). It flies a set
/// arc to a point picked at launch, so you dodge it by moving; a ring on the ground can show
/// where it'll land. It splashes on landing (or on hitting the player on the way) and can leave
/// a puddle that hurts to stand in.
/// </summary>
public partial class Lobbed : Node3D
{
	private const float Gravity = 14f;

	/// <summary>Projectiles that hurt the player this session (for the self-check).</summary>
	public static int PlayerHits { get; set; }
	public static int Thrown { get; set; }

	private Vector3 _velocity;
	private float _damage;
	private float _splash;
	private float _puddleSeconds;
	private float _puddleDps;
	private Color _color;
	private Vector3 _source;
	private Node3D _player;
	private HealthComponent _playerHealth;
	private MeshInstance3D _marker;
	private float _age;
	private float _flight;
	private Vector3 _spin;
	private uint _worldMask = 1;

	private static readonly Dictionary<Color, StandardMaterial3D> Materials = new();

	private static StandardMaterial3D MaterialFor(Color color, float alpha = 1f)
	{
		Color key = new(color, alpha);
		if (Materials.TryGetValue(key, out StandardMaterial3D m))
			return m;
		m = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = key,
			Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
		};
		Materials[key] = m;
		return m;
	}

	/// <summary>Throws one at a landing point, arriving after flightTime seconds.</summary>
	public static Lobbed Throw(Node parent, Vector3 from, Vector3 landAt, float flightTime, RangedAttack attack, Vector3 source)
	{
		flightTime = Mathf.Max(flightTime, 0.15f);
		var lob = new Lobbed
		{
			Name = "Lobbed",
			TopLevel = true,
			_damage = attack.Damage,
			_splash = attack.SplashRadius,
			_puddleSeconds = attack.PuddleSeconds,
			_puddleDps = attack.PuddleDamagePerSecond,
			_color = attack.Color,
			_source = source,
			_flight = flightTime,
			_spin = new Vector3((float)GD.RandRange(-8, 8), (float)GD.RandRange(-8, 8), (float)GD.RandRange(-8, 8)),
		};
		// Solve the launch velocity for a fixed flight time under gravity.
		Vector3 d = landAt - from;
		lob._velocity = new Vector3(d.X / flightTime, (d.Y + 0.5f * Gravity * flightTime * flightTime) / flightTime, d.Z / flightTime);

		Mesh mesh = attack.Cube
			? new BoxMesh { Size = Vector3.One * attack.ProjectileSize }
			: new SphereMesh { Radius = attack.ProjectileSize * 0.5f, Height = attack.ProjectileSize, RadialSegments = 8, Rings = 4 };
		mesh.SurfaceSetMaterial(0, MaterialFor(attack.Color));
		lob.AddChild(new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		parent.AddChild(lob);
		lob.GlobalPosition = from;

		lob._player = parent.GetTree().GetFirstNodeInGroup("player") as Node3D;
		lob._playerHealth = HealthComponent.FindOn(lob._player);

		if (attack.LandingMarker)
		{
			var ring = new TorusMesh { InnerRadius = attack.SplashRadius * 0.85f, OuterRadius = attack.SplashRadius, Rings = 24, RingSegments = 4 };
			ring.SurfaceSetMaterial(0, MaterialFor(attack.Color, 0.6f));
			lob._marker = new MeshInstance3D { Mesh = ring, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
			lob.AddChild(lob._marker);
			lob._marker.GlobalPosition = new Vector3(landAt.X, landAt.Y + 0.05f, landAt.Z);
			lob._marker.Scale = new Vector3(1f, 0.2f, 1f);
		}
		Thrown++;
		return lob;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_age += dt;
		Vector3 from = GlobalPosition;
		_velocity.Y -= Gravity * dt;
		Vector3 to = from + _velocity * dt;

		if (_marker != null)
		{
			// The ring tightens as it comes down: a clear "now" cue.
			float k = Mathf.Clamp(_age / _flight, 0f, 1f);
			float s = Mathf.Lerp(1.6f, 1f, k);
			_marker.Scale = new Vector3(s, 0.2f, s);
		}
		GetChild<MeshInstance3D>(0).Rotation += _spin * dt;

		// Hit the player on the way down (a direct hit), or anything solid.
		if (IsInstanceValid(_player))
		{
			Vector3 chest = _player.GlobalPosition + Vector3.Up * 1.0f;
			if (SegmentDistance(from, to, chest) < 0.7f)
			{
				Land(to, direct: true);
				return;
			}
		}
		var query = PhysicsRayQueryParameters3D.Create(from, to, _worldMask);
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			Land((Vector3)hit["position"], direct: false);
			return;
		}
		GlobalPosition = to;
		if (to.Y < -5f || _age > 6f)
			QueueFree();
	}

	private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
	{
		Vector3 ab = b - a;
		float t = ab.LengthSquared() > 1e-6f ? Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f) : 0f;
		return (a + ab * t).DistanceTo(p);
	}

	private void Land(Vector3 at, bool direct)
	{
		Node effects = GetParent();
		ImpactEffects.SpawnSparks(effects, at, Vector3.Up, _color, 26, 2.2f, 0.5f, 0.8f);
		PlaceholderSfx.PlayAt(effects, PlaceholderSfx.Splat, at, -2f, 0.12f);
		if (_playerHealth != null && !_playerHealth.IsDead && IsInstanceValid(_player))
		{
			Vector3 feet = _player.GlobalPosition;
			float flat = new Vector2(feet.X - at.X, feet.Z - at.Z).Length();
			if (direct || (flat <= _splash && Mathf.Abs(feet.Y - at.Y) < 2f))
			{
				_playerHealth.ApplyDamage(_damage, _source);
				PlayerHits++;
			}
		}
		if (_puddleSeconds > 0f)
			Puddle.Spawn(effects, new Vector3(at.X, at.Y, at.Z), _splash, _puddleSeconds, _puddleDps, _color, _source);
		QueueFree();
	}
}
