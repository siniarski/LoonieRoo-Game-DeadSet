using Godot;

namespace Deadset.Combat;

/// <summary>
/// A collision shape that takes extra damage (a head). Put it directly under the enemy body.
/// Collision shapes can't live under the animated Visual, so it can follow a visual node
/// (the head mesh) instead. It only moves the shape once the head has drifted by more than
/// FollowTolerance, because every shape move makes the physics server refit the body.
/// </summary>
[GlobalClass]
public partial class WeakSpot : CollisionShape3D
{
	[Export] public float DamageMultiplier { get; set; } = 2f;
	/// <summary>Optional node to stick to, usually the head mesh under Visual.</summary>
	[Export] public NodePath FollowPath { get; set; }
	/// <summary>How far the followed node may drift from the shape before the shape catches up, in metres.</summary>
	[Export] public float FollowTolerance { get; set; } = 0.03f;

	private Node3D _follow;
	private Node3D _body;
	private Vector3 _applied;

	public override void _Ready()
	{
		if (FollowPath != null && !FollowPath.IsEmpty)
			_follow = GetNodeOrNull<Node3D>(FollowPath);
		_body = GetParent() as Node3D;
		if (_follow == null || _body == null)
		{
			SetPhysicsProcess(false);
			return;
		}
		_applied = _body.ToLocal(_follow.GlobalPosition);
		Position = _applied;
	}

	public override void _PhysicsProcess(double delta) => Follow();

	/// <summary>
	/// Stops this node ticking itself. The owner then calls Follow() once per physics tick,
	/// after it has moved (one engine callback per enemy instead of two).
	/// </summary>
	public void DriveExternally() => SetPhysicsProcess(false);

	/// <summary>Catches the shape up with the followed node if it has drifted past the tolerance.</summary>
	public void Follow()
	{
		if (_follow == null || _body == null)
			return;
		// Compare in the body's space, so simply running along doesn't count as movement.
		Vector3 local = _body.GlobalTransform.AffineInverse() * _follow.GlobalPosition;
		if (local.DistanceSquaredTo(_applied) <= FollowTolerance * FollowTolerance)
			return;
		_applied = local;
		Position = local;
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
