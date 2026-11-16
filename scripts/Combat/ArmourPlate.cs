using Deadset.Effects;
using Godot;

namespace Deadset.Combat;

/// <summary>
/// A piece of armour bolted onto an enemy (a sheet-metal face plate, a tin collar). It's its own
/// little collision body riding on the model, so shots that hit it stop there instead of reaching
/// the body or head behind. Enough damage knocks it off, and the enemy is soft there from then on.
/// Weapons ask for it with FromHit before they look for health.
/// </summary>
public partial class ArmourPlate : StaticBody3D
{
	[Signal] public delegate void BrokenEventHandler(ArmourPlate plate);

	public float Armour { get; private set; }
	public float MaxArmour { get; private set; }
	/// <summary>Heavy plates shrug off melee; light gear is shredded by it.</summary>
	public bool Heavy { get; private set; }
	public bool CoversHead { get; private set; }
	public bool IsBroken => Armour <= 0f;
	/// <summary>The enemy wearing it.</summary>
	public Node3D Wearer { get; private set; }

	/// <summary>Plates knocked off this session (for the self-check).</summary>
	public static int PlatesBroken { get; set; }
	/// <summary>Shots stopped by a plate this session (for the self-check).</summary>
	public static int ShotsBlocked { get; set; }

	private MeshInstance3D _mesh;
	private float _flash;
	private StandardMaterial3D _flashMaterial;

	/// <summary>Builds the plate's collision on a gear mesh. Call before the wearer enters the tree.</summary>
	public static ArmourPlate Attach(MeshInstance3D mesh, Shape3D shape, Node3D wearer, float armour, bool heavy, bool coversHead)
	{
		var plate = new ArmourPlate
		{
			Name = "Plate",
			Armour = armour,
			MaxArmour = armour,
			Heavy = heavy,
			CoversHead = coversHead,
			Wearer = wearer,
			// Same layer as enemies, so every weapon that hits enemies hits plates; collides with nothing itself.
			CollisionLayer = 4,
			CollisionMask = 0,
			// Stay in the physics world even if the wearer is paused (the self-check freezes enemies).
			DisableMode = DisableModeEnum.KeepActive,
		};
		plate._mesh = mesh;
		plate.AddChild(new CollisionShape3D { Shape = shape });
		mesh.AddChild(plate);
		if (wearer is PhysicsBody3D body)
			body.AddCollisionExceptionWith(plate);
		return plate;
	}

	/// <summary>The plate a ray or shape query hit, or null.</summary>
	public static ArmourPlate FromHit(Node collider) => collider as ArmourPlate;

	/// <summary>
	/// Takes a hit. Returns true if this knocked it off. The hit itself never reaches the wearer.
	/// </summary>
	public bool TakeHit(float damage, Vector3 point, Vector3 normal)
	{
		if (IsBroken)
			return false;
		ShotsBlocked++;
		Armour -= damage;
		Node effects = GetTree().CurrentScene;
		ImpactEffects.SpawnSparks(effects, point, normal, Heavy ? new Color(1f, 0.95f, 0.8f) : new Color(0.85f, 0.85f, 0.85f), Heavy ? 14 : 8);
		PlaceholderSfx.PlayAt(effects, PlaceholderSfx.Ping, point, Heavy ? -2f : -6f, 0.15f);
		Flash();
		if (Armour > 0f)
			return false;
		Break(point);
		return true;
	}

	private void Flash()
	{
		_flashMaterial ??= new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(1f, 1f, 0.85f),
		};
		if (IsInstanceValid(_mesh))
			_mesh.MaterialOverlay = _flashMaterial;
		_flash = 0.05f;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		_flash -= (float)delta;
		if (_flash > 0f)
			return;
		if (IsInstanceValid(_mesh))
			_mesh.MaterialOverlay = null;
		SetProcess(false);
	}

	/// <summary>Knocks the plate off: it clangs, spins away and the wearer is open there.</summary>
	private void Break(Vector3 point)
	{
		PlatesBroken++;
		Armour = 0f;
		Node effects = GetTree().CurrentScene;
		PlaceholderSfx.PlayAt(effects, PlaceholderSfx.Clang, point, 0f, 0.1f);
		ImpactEffects.SpawnSparks(effects, point, Vector3.Up, new Color(1f, 0.8f, 0.4f), 24, 1.6f, 0.4f);
		EmitSignal(SignalName.Broken, this);
		MeshInstance3D mesh = _mesh;
		_mesh = null;
		if (IsInstanceValid(mesh))
		{
			mesh.MaterialOverlay = null;
			Vector3 away = IsInstanceValid(Wearer) ? mesh.GlobalPosition - Wearer.GlobalPosition : Vector3.Forward;
			float ground = IsInstanceValid(Wearer) ? Mathf.Max(Wearer.GlobalPosition.Y, 0f) : 0f;
			if (!Corpse.Spawn(effects, mesh, mesh.GlobalPosition, ground, away, 3.5f, 3.5f, 1.2f, 0.5f))
				mesh.QueueFree();
		}
		QueueFree();
	}
}
