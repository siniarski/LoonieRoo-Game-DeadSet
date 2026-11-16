using Deadset.Combat;
using Godot;

namespace Deadset.Enemies;

/// <summary>A glowing puddle that hurts the player while they stand in it, then dries up.</summary>
public partial class Puddle : Node3D
{
	private float _radius;
	private float _life;
	private float _total;
	private float _dps;
	private Vector3 _source;
	private Node3D _player;
	private HealthComponent _health;
	private float _tick;
	private MeshInstance3D _mesh;

	public static void Spawn(Node parent, Vector3 at, float radius, float seconds, float dps, Color color, Vector3 source)
	{
		var mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.03f, RadialSegments = 16 };
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(color, 0.55f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		});
		var puddle = new Puddle { Name = "Puddle", TopLevel = true, _radius = radius, _life = seconds, _total = seconds, _dps = dps, _source = source };
		puddle._mesh = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		puddle.AddChild(puddle._mesh);
		parent.AddChild(puddle);
		puddle.GlobalPosition = at + Vector3.Up * 0.03f;
		puddle._player = parent.GetTree().GetFirstNodeInGroup("player") as Node3D;
		puddle._health = HealthComponent.FindOn(puddle._player);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_life -= dt;
		if (_life <= 0f)
		{
			QueueFree();
			return;
		}
		float shrink = Mathf.Clamp(_life / Mathf.Min(_total, 0.6f), 0f, 1f);
		_mesh.Scale = new Vector3(shrink, 1f, shrink);
		_tick -= dt;
		if (_tick > 0f || _health == null || _health.IsDead || !IsInstanceValid(_player))
			return;
		_tick = 0.25f;
		Vector3 p = _player.GlobalPosition;
		if (new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() <= _radius * shrink && Mathf.Abs(p.Y - GlobalPosition.Y) < 1f)
			_health.ApplyDamage(_dps * 0.25f, _source);
	}
}
