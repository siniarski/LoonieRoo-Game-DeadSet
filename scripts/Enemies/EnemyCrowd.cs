using System;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// Per-physics-tick snapshot of where every enemy is, bucketed into a coarse grid, so each
/// enemy's separation check only looks at its neighbours instead of the whole horde.
/// Enemy fills it once per tick (see Enemy.BeginTick); nothing here touches the scene tree.
/// </summary>
internal static class EnemyCrowd
{
	/// <summary>Bucket size in metres. Should be at least the largest separation radius for best results.</summary>
	private const float CellSize = 2f;
	/// <summary>Buckets per side. The grid is centred on the target; enemies beyond it share the edge buckets.</summary>
	private const int Side = 64;

	/// <summary>The physics frame the snapshot belongs to.</summary>
	public static ulong Frame { get; private set; } = ulong.MaxValue;
	public static Node3D Target { get; private set; }
	public static Vector3 TargetPosition { get; private set; }

	private static readonly int[] Heads = new int[Side * Side];
	private static int[] _next = new int[256];
	private static Vector3[] _positions = new Vector3[256];
	private static float _originX;
	private static float _originZ;

	public static void Begin(ulong frame, int count, Node3D target, Vector3 targetPosition)
	{
		Frame = frame;
		Target = target;
		TargetPosition = targetPosition;
		_originX = targetPosition.X - Side * CellSize * 0.5f;
		_originZ = targetPosition.Z - Side * CellSize * 0.5f;
		Array.Fill(Heads, -1);
		if (_positions.Length < count)
		{
			int size = Math.Max(count, _positions.Length * 2);
			_positions = new Vector3[size];
			_next = new int[size];
		}
	}

	public static void Add(int index, Vector3 position)
	{
		_positions[index] = position;
		int bucket = BucketZ(position.Z) * Side + BucketX(position.X);
		_next[index] = Heads[bucket];
		Heads[bucket] = index;
	}

	public static Vector3 PositionOf(int index) => _positions[index];

	/// <summary>Sum of pushes away from every other enemy within the radius (1 at zero distance, 0 at the radius).</summary>
	public static Vector3 Separation(int index, float radius)
	{
		Vector3 me = _positions[index];
		Vector3 push = Vector3.Zero;
		int x0 = BucketX(me.X - radius), x1 = BucketX(me.X + radius);
		int z0 = BucketZ(me.Z - radius), z1 = BucketZ(me.Z + radius);
		float radiusSquared = radius * radius;

		for (int z = z0; z <= z1; z++)
		{
			for (int x = x0; x <= x1; x++)
			{
				for (int other = Heads[z * Side + x]; other >= 0; other = _next[other])
				{
					if (other == index)
						continue;
					float ax = me.X - _positions[other].X;
					float az = me.Z - _positions[other].Z;
					float squared = ax * ax + az * az;
					if (squared >= radiusSquared)
						continue;
					float distance = Mathf.Sqrt(squared);
					if (distance < 0.001f)
					{
						// Exactly stacked: pick any way out.
						ax = GD.Randf() - 0.5f;
						az = GD.Randf() - 0.5f;
						float length = Mathf.Sqrt(ax * ax + az * az);
						if (length < 0.0001f)
							continue;
						push.X += ax / length;
						push.Z += az / length;
						continue;
					}
					float weight = (1f - distance / radius) / distance;
					push.X += ax * weight;
					push.Z += az * weight;
				}
			}
		}
		return push;
	}

	private static int BucketX(float x) => Math.Clamp(Mathf.FloorToInt((x - _originX) / CellSize), 0, Side - 1);

	private static int BucketZ(float z) => Math.Clamp(Mathf.FloorToInt((z - _originZ) / CellSize), 0, Side - 1);
}
