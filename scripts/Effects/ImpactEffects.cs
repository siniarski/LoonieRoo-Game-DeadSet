using System.Collections.Generic;
using Godot;

namespace Deadset.Effects;

/// <summary>Spawns short-lived spark bursts built from primitives (no imported assets).</summary>
public static class ImpactEffects
{
	private static readonly Dictionary<Color, BoxMesh> SparkMeshes = new();

	public static void SpawnSparks(Node parent, Vector3 position, Vector3 normal, Color color, int amount = 10,
		float size = 1f, float lifetime = 0.3f, float speed = 1f)
	{
		var particles = new CpuParticles3D
		{
			// CpuParticles3D starts emitting by default. Hold it until it's placed,
			// or the burst fires from the world origin.
			Emitting = false,
			// Place in world space directly, regardless of the parent's transform.
			TopLevel = true,
			Transform = new Transform3D(BasisFacing(normal), position),
			OneShot = true,
			Explosiveness = 1f,
			Amount = amount,
			Lifetime = lifetime,
			Mesh = GetSparkMesh(color),
			Direction = Vector3.Up,
			Spread = 55f,
			InitialVelocityMin = 2.5f * speed,
			InitialVelocityMax = 6.5f * speed,
			Gravity = new Vector3(0f, -14f, 0f),
			ScaleAmountMin = 0.6f * size,
			ScaleAmountMax = 1.2f * size,
		};

		parent.AddChild(particles);
		particles.Finished += particles.QueueFree;
		particles.Emitting = true;
	}

	/// <summary>A basis whose local Up points along the given normal.</summary>
	private static Basis BasisFacing(Vector3 normal)
	{
		normal = normal.Normalized();
		if (normal.Dot(Vector3.Up) < -0.999f)
			return new Basis(Vector3.Right, Mathf.Pi);
		if (normal.Dot(Vector3.Up) > 0.999f)
			return Basis.Identity;
		return new Basis(new Quaternion(Vector3.Up, normal));
	}

	private static BoxMesh GetSparkMesh(Color color)
	{
		if (SparkMeshes.TryGetValue(color, out BoxMesh mesh))
			return mesh;

		mesh = new BoxMesh
		{
			Size = new Vector3(0.03f, 0.03f, 0.03f),
			Material = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = color,
			},
		};
		SparkMeshes[color] = mesh;
		return mesh;
	}
}
