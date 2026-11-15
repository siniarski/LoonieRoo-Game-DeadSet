using Godot;

namespace Deadset.Effects;

/// <summary>
/// A dead enemy's model, knocked away from the shot: it flies back, tips over, lands,
/// then sinks into the ground and is freed. Purely visual, no collision, so a horde of
/// corpses costs only transforms. Past MaxAlive at once, new deaths skip the corpse.
/// </summary>
public partial class Corpse : Node3D
{
	/// <summary>Most corpses on screen at once.</summary>
	public static int MaxAlive { get; set; } = 48;
	private static int _alive;

	private const float Gravity = 22f;
	private const float FlashTime = 0.07f;

	private Vector3 _velocity;
	private Vector3 _tipAxis;
	private float _tipSpeed;
	private float _tipped;
	private float _groundY;
	private float _age;
	private float _settleTime = -1f;
	private float _hold;
	private float _sink;
	private Node3D _model;

	/// <summary>
	/// Takes over the enemy's model and plays the death tumble. Returns false (and does
	/// nothing) when too many corpses are already out.
	/// </summary>
	public static bool Spawn(Node parent, Node3D model, Vector3 position, float groundY, Vector3 knockDirection,
		float knockSpeed, float upSpeed, float hold, float sink)
	{
		if (_alive >= MaxAlive || model == null || parent == null)
			return false;

		var corpse = new Corpse { Name = "Corpse" };
		parent.AddChild(corpse);
		corpse.GlobalPosition = position;
		model.Reparent(corpse, keepGlobalTransform: true);

		knockDirection.Y = 0f;
		knockDirection = knockDirection.LengthSquared() > 0.0001f ? knockDirection.Normalized() : Vector3.Forward;
		float jitter = (float)GD.RandRange(0.8, 1.2);
		corpse._model = model;
		corpse._groundY = groundY;
		corpse._velocity = knockDirection * knockSpeed * jitter + Vector3.Up * upSpeed * jitter;
		// Tip backwards along the knock, with a little random twist so a flock doesn't fall in lockstep.
		Vector3 axis = Vector3.Up.Cross(knockDirection).Rotated(Vector3.Up, (float)GD.RandRange(-0.5, 0.5));
		corpse._tipAxis = axis.Normalized();
		corpse._tipSpeed = (float)GD.RandRange(7.0, 11.0);
		corpse._hold = hold;
		corpse._sink = sink;
		_alive++;
		return true;
	}

	public override void _ExitTree() => _alive--;

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;

		if (_age >= FlashTime && _age - dt < FlashTime)
			ClearFlash(_model);

		if (_settleTime < 0f)
		{
			_velocity.Y -= Gravity * dt;
			Vector3 p = GlobalPosition + _velocity * dt;
			if (p.Y <= _groundY && _velocity.Y < 0f)
			{
				p.Y = _groundY;
				_velocity = Vector3.Zero;
				if (_tipped >= Mathf.Pi * 0.45f)
					_settleTime = _age;
			}
			GlobalPosition = p;

			if (_tipped < Mathf.Pi * 0.5f)
			{
				float step = Mathf.Min(_tipSpeed * dt, Mathf.Pi * 0.5f - _tipped);
				_tipped += step;
				GlobalBasis = new Basis(_tipAxis, step) * GlobalBasis;
			}
			return;
		}

		float t = _age - _settleTime - _hold;
		if (t <= 0f)
			return;
		float k = Mathf.Clamp(t / Mathf.Max(_sink, 0.01f), 0f, 1f);
		Vector3 pos = GlobalPosition;
		pos.Y = _groundY - 0.7f * k * k;
		GlobalPosition = pos;
		if (k >= 1f)
			QueueFree();
	}

	private static void ClearFlash(Node node)
	{
		if (node is GeometryInstance3D geometry)
			geometry.MaterialOverlay = null;
		foreach (Node child in node.GetChildren())
			ClearFlash(child);
	}
}
