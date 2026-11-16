using Godot;

namespace Deadset.Enemies;

/// <summary>
/// One piece of a variant's visible kit, built from a primitive at spawn (no imported art).
/// With Armour above zero it is a plate: shots that hit it are stopped (rivets punch through),
/// and enough damage knocks it off.
/// </summary>
[GlobalClass]
public partial class GearPiece : Resource
{
	public enum ShapeKind { Box, Cylinder, Torus, Sphere, Prism }

	[Export] public ShapeKind Shape { get; set; } = ShapeKind.Box;
	/// <summary>Box: full size. Cylinder: x = radius, y = height. Torus: x = outer radius, y = ring thickness. Sphere: x = radius.</summary>
	[Export] public Vector3 Size { get; set; } = new(0.2f, 0.2f, 0.2f);
	/// <summary>The model node it rides on, e.g. "Visual/Striker/Head".</summary>
	[Export] public NodePath Attach { get; set; } = "Visual";
	[Export] public Vector3 Offset { get; set; }
	[Export] public Vector3 RotationDegrees { get; set; }
	[Export] public Color Color { get; set; } = new(0.5f, 0.5f, 0.5f);
	/// <summary>Glows (unshaded), for tells that must read in the dusk.</summary>
	[Export] public bool Glow { get; set; }
	/// <summary>Only shown once the enemy enrages (red wattles).</summary>
	[Export] public bool ShowWhenEnraged { get; set; }

	[ExportGroup("Armour")]
	/// <summary>Damage the plate soaks before it's knocked off (0 = cosmetic).</summary>
	[Export] public float Armour { get; set; }
	/// <summary>Heavy plates (sheet metal) shrug off melee; light gear (wire, ties, wraps) gets shredded by it.</summary>
	[Export] public bool Heavy { get; set; }
	/// <summary>Covers the head: a melee head hit lands on the plate instead.</summary>
	[Export] public bool CoversHead { get; set; }
}
