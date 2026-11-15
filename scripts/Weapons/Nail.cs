using Deadset.Combat;
using Deadset.Effects;
using Godot;
using Godot.Collections;

namespace Deadset.Weapons;

/// <summary>
/// A fast, visible projectile. Hit-tests its whole path every physics step so it can't
/// tunnel through thin targets. Damages anything with a HealthComponent, otherwise
/// sticks into the surface for a few seconds.
/// </summary>
public partial class Nail : Node3D
{
	public struct LaunchParams
	{
		/// <summary>Where hit-testing starts (the camera), so close-range shots land where you aim.</summary>
		public Vector3 Origin;
		public Vector3 Direction;
		public float Speed;
		public float Damage;
		public float MaxRange;
		/// <summary>Where the nail is drawn from (the muzzle). It converges onto the true path.</summary>
		public Vector3 VisualStart;
		public Weapon Source;
		public Rid Exclude;
		/// <summary>How many times the nail bounces off the world before sticking.</summary>
		public int Ricochets;
		/// <summary>Enemies the nail passes straight through before stopping.</summary>
		public int Pierce;
		/// <summary>Share of damage kept after each pierce or chain jump.</summary>
		public float PierceDamageKeep;
		/// <summary>Times the nail jumps from a hit enemy to another one nearby.</summary>
		public int Chain;
	}

	[Export] public float StuckLifetime { get; set; } = 4f;
	/// <summary>Seconds for the drawn nail to slide from the muzzle onto the aim line.</summary>
	[Export] public float VisualConvergeTime { get; set; } = 0.06f;
	/// <summary>How deep a stuck nail sits in the surface, in metres.</summary>
	[Export] public float EmbedDepth { get; set; } = 0.1f;
	/// <summary>Share of its speed a nail keeps after a ricochet.</summary>
	[Export(PropertyHint.Range, "0.1,1")] public float RicochetSpeedKeep { get; set; } = 0.8f;
	/// <summary>Share of its remaining range a nail keeps after a ricochet.</summary>
	[Export(PropertyHint.Range, "0.05,1")] public float RicochetRangeKeep { get; set; } = 0.4f;
	[Export] public Color RicochetSparkColor { get; set; } = new(0.6f, 0.9f, 1f);
	/// <summary>How far a chain jump looks for its next enemy, in metres.</summary>
	[Export] public float ChainRange { get; set; } = 6f;
	[Export] public Color ChainSparkColor { get; set; } = new(0.75f, 0.5f, 1f);
	[Export(PropertyHint.Layers3DPhysics)] public uint CollisionMask { get; set; } = 5;  // world + enemies
	[Export] public Color WallSparkColor { get; set; } = new(1f, 0.7f, 0.3f);
	[Export] public Color FleshSparkColor { get; set; } = new(1f, 1f, 0.85f);
	[Export] public Color WeakSpotSparkColor { get; set; } = new(1f, 0.85f, 0.2f);

	private Node3D _mesh;
	private Vector3 _velocity;
	private Vector3 _direction;
	private float _damage;
	private float _remainingRange;
	private int _ricochetsLeft;
	private int _pierceLeft;
	private int _chainLeft;
	private float _pierceKeep;
	private readonly System.Collections.Generic.HashSet<HealthComponent> _alreadyHit = new();
	private Weapon _source;
	private Array<Rid> _exclude;
	private Vector3 _visualOffset;
	private float _age;
	private bool _stuck;
	private float _stuckTimer;

	public override void _Ready()
	{
		_mesh = GetNode<Node3D>("Mesh");
	}

	public void Launch(LaunchParams p)
	{
		_direction = p.Direction.Normalized();
		_velocity = _direction * p.Speed;
		_damage = p.Damage;
		_remainingRange = p.MaxRange;
		_ricochetsLeft = p.Ricochets;
		_pierceLeft = p.Pierce;
		_chainLeft = p.Chain;
		_pierceKeep = p.PierceDamageKeep;
		_source = p.Source;
		_exclude = new Array<Rid>();
		if (p.Exclude.IsValid)
			_exclude.Add(p.Exclude);

		GlobalPosition = p.Origin;
		Vector3 up = Mathf.Abs(_direction.Y) > 0.99f ? Vector3.Right : Vector3.Up;
		LookAt(p.Origin + _direction, up);

		_visualOffset = p.VisualStart - p.Origin;
		_mesh.GlobalPosition = p.VisualStart;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_stuck)
		{
			_stuckTimer -= dt;
			if (_stuckTimer <= 0f)
				QueueFree();
			return;
		}

		Vector3 from = GlobalPosition;
		Vector3 step = _velocity * dt;
		_remainingRange -= step.Length();

		var query = PhysicsRayQueryParameters3D.Create(from, from + step, CollisionMask, _exclude);
		Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (hit.Count > 0)
		{
			OnImpact((Vector3)hit["position"], (Vector3)hit["normal"], hit["collider"].As<Node>(), (int)hit["shape"]);
			return;
		}

		GlobalPosition = from + step;
		UpdateVisualOffset(dt);

		if (_remainingRange <= 0f)
			QueueFree();
	}

	private void UpdateVisualOffset(float dt)
	{
		_age += dt;
		float remaining = 1f - Mathf.Clamp(_age / VisualConvergeTime, 0f, 1f);
		_mesh.GlobalPosition = GlobalPosition + _visualOffset * remaining;
	}

	private void OnImpact(Vector3 point, Vector3 normal, Node collider, int shapeIndex)
	{
		Node effectsParent = GetParent();
		HealthComponent health = HealthComponent.FindOn(collider);

		if (health != null)
		{
			WeakSpot weakSpot = WeakSpot.FromHit(collider, shapeIndex);
			if (!health.IsDead && _alreadyHit.Add(health))
			{
				float damage = _damage * (weakSpot?.DamageMultiplier ?? 1f);
				bool killed = health.ApplyDamage(damage);
				if (IsInstanceValid(_source))
					_source.ReportHit(killed, weakSpot != null);
			}
			ImpactEffects.SpawnSparks(effectsParent, point, normal,
				weakSpot != null ? WeakSpotSparkColor : FleshSparkColor, weakSpot != null ? 22 : 12);

			// Keep going: straight through first, then jump to a neighbour.
			if (collider is CollisionObject3D body)
				_exclude.Add(body.GetRid());
			if (_pierceLeft > 0)
			{
				_pierceLeft--;
				_damage *= _pierceKeep;
				GlobalPosition = point + _direction * 0.05f;
				return;
			}
			if (_chainLeft > 0 && ChainTo(point, effectsParent))
				return;
			QueueFree();
			return;
		}

		if (_ricochetsLeft > 0)
		{
			Ricochet(point, normal, effectsParent);
			return;
		}

		// Hit the world: stick in, leaving the back half of the nail poking out.
		_stuck = true;
		_stuckTimer = StuckLifetime;
		GlobalPosition = point + _direction * EmbedDepth;
		_mesh.Position = Vector3.Zero;
		ImpactEffects.SpawnSparks(effectsParent, point, normal, WallSparkColor, 8);
		PlaceholderSfx.PlayAt(effectsParent, PlaceholderSfx.NailImpact, point, -10f);
	}

	/// <summary>
	/// Redirects the nail from the enemy it just hit to the nearest other live enemy within
	/// ChainRange, aiming at its head. Returns false if there is nobody to jump to.
	/// </summary>
	private bool ChainTo(Vector3 point, Node effectsParent)
	{
		Node3D best = null;
		Vector3 bestAim = default;
		float bestDistance = ChainRange * ChainRange;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemy || enemy.IsQueuedForDeletion())
				continue;
			HealthComponent health = HealthComponent.FindOn(enemy);
			if (health == null || health.IsDead || _alreadyHit.Contains(health))
				continue;
			Vector3 aim = enemy.GetNodeOrNull<Node3D>("HeadWeakSpot")?.GlobalPosition ?? enemy.GlobalPosition + Vector3.Up;
			float distance = aim.DistanceSquaredTo(point);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = enemy;
				bestAim = aim;
			}
		}
		if (best == null)
			return false;

		_chainLeft--;
		_damage *= _pierceKeep;
		_direction = (bestAim - point).Normalized();
		_velocity = _direction * _velocity.Length();
		_remainingRange = ChainRange * 1.5f;
		GlobalPosition = point;
		Vector3 up = Mathf.Abs(_direction.Y) > 0.99f ? Vector3.Right : Vector3.Up;
		LookAt(GlobalPosition + _direction, up);
		_visualOffset = Vector3.Zero;
		_mesh.Position = Vector3.Zero;
		ImpactEffects.SpawnSparks(effectsParent, point, _direction, ChainSparkColor, 10);
		PlaceholderSfx.PlayAt(effectsParent, PlaceholderSfx.Ricochet, point, -10f, 0.2f);
		return true;
	}

	/// <summary>Bounce off a world surface: reflect, lose some speed and range, carry on.</summary>
	private void Ricochet(Vector3 point, Vector3 normal, Node effectsParent)
	{
		_ricochetsLeft--;
		_direction = _direction.Bounce(normal).Normalized();
		_velocity = _direction * _velocity.Length() * RicochetSpeedKeep;
		_remainingRange *= RicochetRangeKeep;

		// Lift off the surface a touch so the next ray doesn't start inside it.
		GlobalPosition = point + normal * 0.02f;
		Vector3 up = Mathf.Abs(_direction.Y) > 0.99f ? Vector3.Right : Vector3.Up;
		LookAt(GlobalPosition + _direction, up);
		_visualOffset = Vector3.Zero;
		_mesh.Position = Vector3.Zero;

		ImpactEffects.SpawnSparks(effectsParent, point, normal, RicochetSparkColor, 10);
		PlaceholderSfx.PlayAt(effectsParent, PlaceholderSfx.Ricochet, point, -8f, 0.15f);
	}
}
