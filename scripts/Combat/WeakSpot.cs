using Godot;

namespace Deadset.Combat;

/// <summary>
/// A collision shape that takes extra damage (a head). Put it directly under the enemy body.
/// Collision shapes can't live under the animated Visual, so it can follow a visual node
/// (the head mesh) every physics step instead.
/// </summary>
[GlobalClass]
public partial class WeakSpot : CollisionShape3D
{
	[Export] public float DamageMultiplier { get; set; } = 2f;
	/// <summary>Optional node to stick to, usually the head mesh under Visual.</summary>
	[Export] public NodePath FollowPath { get; set; }

	private Node3D _follow;

	public override void _Ready()
	{
		if (FollowPath != null && !FollowPath.IsEmpty)
			_follow = GetNodeOrNull<Node3D>(FollowPath);
		if (_follow != null)
			GlobalPosition = _follow.GlobalPosition;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_follow != null)
			GlobalPosition = _follow.GlobalPosition;
	}

	/// <summary>The weak spot a ray hit, or null if it hit some other shape.</summary>
	public static WeakSpot FromHit(Node collider, int shapeIndex)
	{
		if (collider is not CollisionObject3D body || shapeIndex < 0)
			return null;
		uint owner = body.ShapeFindOwner(shapeIndex);
		return body.ShapeOwnerGetOwner(owner) as WeakSpot;
	}
}
