using System.Collections.Generic;
using Godot;

namespace Deadset.Combat;

/// <summary>
/// Visual "you hit it" feedback: a white flash and a quick squash on every damage tick.
/// Reusable on dummies and enemies. Needs a sibling HealthComponent and a Visual node.
/// </summary>
public partial class HitReaction : Node
{
	[Export] public NodePath HealthPath { get; set; } = "../HealthComponent";
	[Export] public NodePath VisualPath { get; set; } = "../Visual";
	[Export] public float FlashTime { get; set; } = 0.06f;
	[Export] public Vector3 SquashScale { get; set; } = new(1.12f, 0.88f, 1.12f);
	[Export] public float SquashRecoverSpeed { get; set; } = 18f;

	private static StandardMaterial3D _flashMaterial;

	private readonly List<GeometryInstance3D> _meshes = new();
	private Node3D _visual;
	private Vector3 _restScale;
	private float _flashTimer;
	private float _squash;

	public override void _Ready()
	{
		_flashMaterial ??= new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
		};

		_visual = GetNode<Node3D>(VisualPath);
		_restScale = _visual.Scale;
		CollectMeshes(_visual);
		GetNode<HealthComponent>(HealthPath).Damaged += OnDamaged;
		// Idle until something is hit.
		SetProcess(false);
	}

	private void CollectMeshes(Node node)
	{
		if (node is GeometryInstance3D geometry)
			_meshes.Add(geometry);
		foreach (Node child in node.GetChildren())
			CollectMeshes(child);
	}

	private void OnDamaged(float amount, float remaining)
	{
		_flashTimer = FlashTime;
		_squash = 1f;
		SetFlash(true);
		SetProcess(true);
	}

	private void SetFlash(bool on)
	{
		// Armour knocked off takes its mesh with it: skip meshes that have left the model.
		foreach (GeometryInstance3D mesh in _meshes)
			if (IsInstanceValid(mesh) && _visual.IsAncestorOf(mesh))
				mesh.MaterialOverlay = on ? _flashMaterial : null;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (_flashTimer > 0f)
		{
			_flashTimer -= dt;
			if (_flashTimer <= 0f)
				SetFlash(false);
		}

		if (_squash > 0.001f)
		{
			_squash *= Mathf.Exp(-SquashRecoverSpeed * dt);
			_visual.Scale = _restScale * Vector3.One.Lerp(SquashScale, _squash);
		}
		else
		{
			if (_squash > 0f)
			{
				_squash = 0f;
				_visual.Scale = _restScale;
			}
			if (_flashTimer <= 0f)
				SetProcess(false);
		}
	}
}
