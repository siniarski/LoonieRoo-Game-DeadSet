using System;
using System.Collections.Generic;
using Godot;

namespace Deadset.Enemies;

/// <summary>
/// One shared flow field for every enemy: a flat grid cut from the baked navmesh, holding the
/// walking distance from each cell to the target (the player). An enemy asks for a route and
/// gets either "run straight at them" (a clear line across walkable cells) or a waypoint a few
/// cells down the field. One distance fill per target move replaces a path query per enemy.
/// Assumes a single ground level: navmesh polygons more than GroundBand above the lowest
/// vertex (the roofs of cover) are ignored.
/// </summary>
public static class NavGrid
{
	public enum Route
	{
		/// <summary>Nothing in the way (or nothing known): run straight at the target.</summary>
		Direct,
		/// <summary>Head for the waypoint. The straight line may or may not be clear.</summary>
		Waypoint,
		/// <summary>Head for the waypoint. Cover is known to block the straight line.</summary>
		Blocked,
	}

	/// <summary>Grid cell size in metres. Smaller follows cover more tightly but costs more per fill.</summary>
	public static float CellSize { get; set; } = 0.5f;
	/// <summary>Navmesh polygons higher than this above the lowest vertex are not ground.</summary>
	public static float GroundBand { get; set; } = 0.75f;
	/// <summary>How far (in cells) to look for walkable ground when the target is off the navmesh.</summary>
	public static int SnapCells { get; set; } = 8;
	/// <summary>How many cells down the field an enemy looks for the furthest waypoint it can see.</summary>
	public static int LookAheadCells { get; set; } = 16;
	/// <summary>Cells of clear navmesh needed all round a cell for it to count as open ground.</summary>
	public static int OpenMarginCells { get; set; } = 1;
	/// <summary>Least physics frames between distance fills while the target keeps moving.</summary>
	public static int RefillFrames { get; set; } = 6;

	public static bool Ready => _walkable != null && Main.Goal >= 0;
	/// <summary>The flank field points somewhere (see UpdateFlank).</summary>
	public static bool FlankReady => _walkable != null && Flank.Goal >= 0;
	/// <summary>Where the flank field leads, when FlankReady.</summary>
	public static Vector3 FlankGoal => Flank.GoalPosition;
	/// <summary>The walkable grid exists (the arena is baked), whether or not anything is chasing yet.</summary>
	public static bool Built => _walkable != null;

	private const int Unreachable = int.MaxValue;
	private const int StraightCost = 10;
	private const int DiagonalCost = 14;

	/// <summary>A distance field towards one goal.</summary>
	private sealed class Field
	{
		public int[] Distance;
		public int Goal = -1;
		public bool GoalIsTargetCell;
		public Vector3 GoalPosition;
		public int TargetCell = -2;
		public ulong LastFillFrame;
		/// <summary>A disc the field routes round (the player, for the flank field). Radius 0 = none.</summary>
		public float AvoidX, AvoidZ, AvoidRadius;

		public bool InAvoid(float x, float z) =>
			AvoidRadius > 0f && (x - AvoidX) * (x - AvoidX) + (z - AvoidZ) * (z - AvoidZ) < AvoidRadius * AvoidRadius;

		/// <summary>True if the segment passes through the avoided disc.</summary>
		public bool CrossesAvoid(float x0, float z0, float x1, float z1)
		{
			if (AvoidRadius <= 0f)
				return false;
			float dx = x1 - x0, dz = z1 - z0;
			float length2 = dx * dx + dz * dz;
			float t = length2 > 0.0001f ? Mathf.Clamp(((AvoidX - x0) * dx + (AvoidZ - z0) * dz) / length2, 0f, 1f) : 0f;
			return InAvoid(x0 + dx * t, z0 + dz * t);
		}

		public void Reset()
		{
			Distance = null;
			Goal = -1;
			TargetCell = -2;
			LastFillFrame = 0;
		}
	}

	/// <summary>Leads to the target (the player).</summary>
	private static readonly Field Main = new();
	/// <summary>Leads to a point behind the player, for flankers.</summary>
	private static readonly Field Flank = new();

	private static bool[] _walkable;
	private static bool[] _open;
	private static int _width;
	private static int _height;
	private static float _originX;
	private static float _originZ;
	private static float _inverseCell;

	private static readonly PriorityQueue<int, int> Open = new();

	/// <summary>Rebuilds the grid from a baked navmesh. Call again whenever the navmesh changes.</summary>
	public static void Build(NavigationMesh mesh, Transform3D transform)
	{
		Clear();
		if (mesh == null)
			return;
		Vector3[] vertices = mesh.GetVertices();
		int polygonCount = mesh.GetPolygonCount();
		if (vertices.Length == 0 || polygonCount == 0)
			return;

		float minX = float.MaxValue, minZ = float.MaxValue, minY = float.MaxValue;
		float maxX = float.MinValue, maxZ = float.MinValue;
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 v = transform * vertices[i];
			vertices[i] = v;
			minX = Mathf.Min(minX, v.X);
			maxX = Mathf.Max(maxX, v.X);
			minZ = Mathf.Min(minZ, v.Z);
			maxZ = Mathf.Max(maxZ, v.Z);
			minY = Mathf.Min(minY, v.Y);
		}

		float cell = Mathf.Max(CellSize, 0.1f);
		_inverseCell = 1f / cell;
		// One spare cell all round: the border is never walkable, so neighbour lookups need no bounds checks.
		_originX = minX - cell;
		_originZ = minZ - cell;
		_width = Mathf.CeilToInt((maxX - minX) * _inverseCell) + 2;
		_height = Mathf.CeilToInt((maxZ - minZ) * _inverseCell) + 2;
		var walkable = new bool[_width * _height];

		var xs = new List<float>();
		var zs = new List<float>();
		for (int p = 0; p < polygonCount; p++)
		{
			int[] polygon = mesh.GetPolygon(p);
			xs.Clear();
			zs.Clear();
			bool ground = true;
			float pMinX = float.MaxValue, pMaxX = float.MinValue, pMinZ = float.MaxValue, pMaxZ = float.MinValue;
			foreach (int index in polygon)
			{
				Vector3 v = vertices[index];
				ground &= v.Y <= minY + GroundBand;
				xs.Add(v.X);
				zs.Add(v.Z);
				pMinX = Mathf.Min(pMinX, v.X);
				pMaxX = Mathf.Max(pMaxX, v.X);
				pMinZ = Mathf.Min(pMinZ, v.Z);
				pMaxZ = Mathf.Max(pMaxZ, v.Z);
			}
			if (!ground || xs.Count < 3)
				continue;

			int x0 = Mathf.Max(1, Mathf.FloorToInt((pMinX - _originX) * _inverseCell));
			int x1 = Mathf.Min(_width - 2, Mathf.FloorToInt((pMaxX - _originX) * _inverseCell));
			int z0 = Mathf.Max(1, Mathf.FloorToInt((pMinZ - _originZ) * _inverseCell));
			int z1 = Mathf.Min(_height - 2, Mathf.FloorToInt((pMaxZ - _originZ) * _inverseCell));
			for (int z = z0; z <= z1; z++)
			{
				float cz = _originZ + (z + 0.5f) * cell;
				for (int x = x0; x <= x1; x++)
				{
					if (!walkable[z * _width + x] && InsideConvex(xs, zs, _originX + (x + 0.5f) * cell, cz))
						walkable[z * _width + x] = true;
				}
			}
		}

		// Open ground: walkable with OpenMarginCells of walkable cells all round.
		int margin = Mathf.Max(OpenMarginCells, 0);
		var open = new bool[walkable.Length];
		for (int z = margin; z < _height - margin; z++)
		{
			for (int x = margin; x < _width - margin; x++)
			{
				bool clear = true;
				for (int dz = -margin; dz <= margin && clear; dz++)
					for (int dx = -margin; dx <= margin && clear; dx++)
						clear = walkable[(z + dz) * _width + x + dx];
				open[z * _width + x] = clear;
			}
		}

		Main.Distance = new int[walkable.Length];
		Flank.Distance = new int[walkable.Length];
		Array.Fill(Main.Distance, Unreachable);
		Array.Fill(Flank.Distance, Unreachable);
		_open = open;
		_walkable = walkable;
	}

	/// <summary>
	/// True if the point is on navmesh with nothing but navmesh for OpenMarginCells around it:
	/// no cover or wall within reach of a body there.
	/// </summary>
	public static bool IsOpenGround(Vector3 point)
	{
		if (_open == null)
			return false;
		int cell = CellAt(point.X, point.Z);
		return cell >= 0 && _open[cell];
	}

	/// <summary>
	/// Moves a point that is off the ground navmesh (outside the walls, inside cover) to the
	/// nearest spot on it, keeping its height. Points already on the navmesh, and any point
	/// while there is no grid, come back unchanged. For placing spawns.
	/// </summary>
	public static Vector3 SnapToGround(Vector3 point, float maxDistance = 8f)
	{
		if (_walkable == null)
			return point;
		int cell = CellAt(point.X, point.Z);
		if (cell >= 0 && _walkable[cell])
			return point;
		int near = NearestWalkable(point.X, point.Z, Mathf.CeilToInt(maxDistance * _inverseCell), false);
		if (near < 0)
			return point;
		Vector3 centre = CellCentre(near);
		return new Vector3(centre.X, point.Y, centre.Z);
	}

	/// <summary>Forgets the grid (the arena is going away). Enemies fall back to running straight.</summary>
	public static void Clear()
	{
		_walkable = null;
		_open = null;
		Main.Reset();
		Flank.Reset();
	}

	/// <summary>Points the field at the target. Cheap unless the target has moved to another cell.</summary>
	public static void UpdateTarget(Vector3 target, ulong physicsFrame) => Point(Main, target, physicsFrame, RefillFrames, true);

	/// <summary>
	/// Points the flank field at a spot (behind the player). Refills at most every
	/// minFrames physics frames; it only needs to be roughly right.
	/// </summary>
	public static void UpdateFlank(Vector3 point, Vector3 avoid, float avoidRadius, ulong physicsFrame, int minFrames = 20)
	{
		if (Flank.TargetCell != -2 && physicsFrame - Flank.LastFillFrame < (ulong)Mathf.Max(minFrames, 1))
			return;
		Flank.AvoidX = avoid.X;
		Flank.AvoidZ = avoid.Z;
		Flank.AvoidRadius = avoidRadius;
		// The avoided disc moves with the player, so refill even if the goal cell is the same.
		Flank.TargetCell = -3;
		Flank.Goal = -1;
		Point(Flank, point, physicsFrame, minFrames, false);
	}

	/// <summary>Stops the flank field leading anywhere (no flanking wanted right now).</summary>
	public static void ClearFlank()
	{
		Flank.Goal = -1;
		Flank.TargetCell = -2;
	}

	private static void Point(Field field, Vector3 target, ulong physicsFrame, int minFrames, bool exactTarget)
	{
		if (_walkable == null)
			return;
		int cell = CellAt(target.X, target.Z);
		if (cell == field.TargetCell)
		{
			if (field.GoalIsTargetCell)
				field.GoalPosition = target;
			return;
		}
		if (field.TargetCell != -2 && physicsFrame - field.LastFillFrame < (ulong)Mathf.Max(minFrames, 1))
			return;

		field.TargetCell = cell;
		field.LastFillFrame = physicsFrame;
		int goal;
		if (exactTarget)
			goal = cell >= 0 && _walkable[cell] ? cell : NearestWalkable(target.X, target.Z, SnapCells, false);
		else
			// A spot the main field can reach (not a sealed pocket), outside the avoided disc.
			goal = NearestWalkable(target.X, target.Z, SnapCells, true, Main.Distance, field);
		field.GoalIsTargetCell = exactTarget && goal >= 0 && goal == cell;
		if (goal != field.Goal)
		{
			field.Goal = goal;
			Fill(field);
		}
		if (field.Goal >= 0)
			field.GoalPosition = field.GoalIsTargetCell ? target : CellCentre(field.Goal);
	}

	/// <summary>
	/// Where an enemy standing at <paramref name="from"/> should head. Direct means straight at
	/// the target; otherwise <paramref name="waypoint"/> is a point on the way round the cover.
	/// </summary>
	public static Route FindRoute(Vector3 from, out Vector3 waypoint) => FindRoute(Main, from, out waypoint);


	/// <summary>
	/// Like FindRoute, but towards the flank goal. Direct means it is in plain view of the goal
	/// (or there is no flank field): the flanker can stop flanking.
	/// </summary>
	public static bool FindFlankRoute(Vector3 from, out Vector3 waypoint)
	{
		waypoint = default;
		if (_walkable == null || Flank.Goal < 0)
			return false;
		int[] distance = Flank.Distance;
		Vector3 goal = Flank.GoalPosition;
		int cell = CellAt(from.X, from.Z);
		if (cell < 0 || !_walkable[cell])
		{
			int near = NearestWalkable(from.X, from.Z, 3, true, distance);
			if (near < 0)
				return false;
			waypoint = CellCentre(near);
			return true;
		}
		if (distance[cell] == Unreachable)
		{
			// Brushing the edge of the disc while going round: step back out to the field.
			// Deeper in, or the only way is through the player: no way round from here.
			int near = NearestWalkable(from.X, from.Z, 3, true, distance);
			if (near < 0)
				return false;
			waypoint = CellCentre(near);
			return true;
		}
		if (ClearLine(from.X, from.Z, goal.X, goal.Z) && !Flank.CrossesAvoid(from.X, from.Z, goal.X, goal.Z))
		{
			waypoint = goal;
			return true;
		}
		int current = cell;
		int best = -1;
		for (int step = 0; step < LookAheadCells; step++)
		{
			int next = Downhill(current, distance);
			if (next < 0)
				break;
			current = next;
			Vector3 centre = CellCentre(current);
			if (best >= 0 && (!ClearLine(from.X, from.Z, centre.X, centre.Z) || Flank.CrossesAvoid(from.X, from.Z, centre.X, centre.Z)))
				break;
			best = current;
		}
		if (best < 0)
			return false;
		waypoint = CellCentre(best);
		return true;
	}

	private static Route FindRoute(Field field, Vector3 from, out Vector3 waypoint)
	{
		waypoint = default;
		if (_walkable == null || field.Goal < 0)
			return Route.Direct;
		int[] distance = field.Distance;
		Vector3 goalPosition = field.GoalPosition;
		bool goalIsTargetCell = field.GoalIsTargetCell;

		int cell = CellAt(from.X, from.Z);
		if (cell < 0 || !_walkable[cell])
		{
			// Off the navmesh (hugging cover or a wall): step back onto the nearest useful cell.
			int near = NearestWalkable(from.X, from.Z, 3, true, distance);
			if (near < 0)
				return Route.Direct;
			waypoint = CellCentre(near);
			// Pressed up against cover with the target on the far side: get back on the navmesh first.
			bool coverBetween = goalIsTargetCell && !ClearLine(waypoint.X, waypoint.Z, goalPosition.X, goalPosition.Z);
			return coverBetween ? Route.Blocked : Route.Waypoint;
		}
		if (distance[cell] == Unreachable)
			return Route.Direct;

		if (ClearLine(from.X, from.Z, goalPosition.X, goalPosition.Z))
		{
			if (goalIsTargetCell)
				return Route.Direct;
			waypoint = goalPosition;
			return Route.Waypoint;
		}

		// Walk down the field and keep the furthest cell still in plain view. That cuts the
		// corners off the grid path, so enemies round cover in straight runs.
		int current = cell;
		int best = -1;
		for (int step = 0; step < LookAheadCells; step++)
		{
			int next = Downhill(current, distance);
			if (next < 0)
				break;
			current = next;
			Vector3 centre = CellCentre(current);
			if (best >= 0 && !ClearLine(from.X, from.Z, centre.X, centre.Z))
				break;
			best = current;
		}
		if (best < 0)
			return Route.Direct;
		waypoint = CellCentre(best);
		return goalIsTargetCell ? Route.Blocked : Route.Waypoint;
	}

	private static int CellAt(float x, float z)
	{
		int cx = Mathf.FloorToInt((x - _originX) * _inverseCell);
		int cz = Mathf.FloorToInt((z - _originZ) * _inverseCell);
		if (cx < 0 || cz < 0 || cx >= _width || cz >= _height)
			return -1;
		return cz * _width + cx;
	}

	private static Vector3 CellCentre(int cell)
	{
		float size = 1f / _inverseCell;
		return new Vector3(_originX + (cell % _width + 0.5f) * size, 0f, _originZ + (cell / _width + 0.5f) * size);
	}

	/// <summary>Closest walkable cell to a point within a square of cells, or -1.</summary>
	private static int NearestWalkable(float x, float z, int radius, bool needReachable, int[] field = null, Field avoid = null)
	{
		int cx = Mathf.FloorToInt((x - _originX) * _inverseCell);
		int cz = Mathf.FloorToInt((z - _originZ) * _inverseCell);
		float size = 1f / _inverseCell;
		int best = -1;
		float bestDistance = float.MaxValue;
		for (int dz = -radius; dz <= radius; dz++)
		{
			int pz = cz + dz;
			if (pz < 1 || pz >= _height - 1)
				continue;
			for (int dx = -radius; dx <= radius; dx++)
			{
				int px = cx + dx;
				if (px < 1 || px >= _width - 1)
					continue;
				int cell = pz * _width + px;
				if (!_walkable[cell] || (needReachable && (field ?? Main.Distance)[cell] == Unreachable))
					continue;
				float ox = _originX + (px + 0.5f) * size - x;
				float oz = _originZ + (pz + 0.5f) * size - z;
				if (avoid != null && avoid.InAvoid(ox + x, oz + z))
					continue;
				float distance = ox * ox + oz * oz;
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = cell;
				}
			}
		}
		return best;
	}

	/// <summary>The neighbour one step closer to the goal, or -1 at the goal.</summary>
	private static int Downhill(int cell, int[] distance)
	{
		int best = -1;
		int bestDistance = distance[cell];
		for (int dz = -1; dz <= 1; dz++)
		{
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dz == 0)
					continue;
				int next = cell + dz * _width + dx;
				if (!_walkable[next] || distance[next] >= bestDistance)
					continue;
				// No squeezing diagonally past the corner of cover.
				if (dx != 0 && dz != 0 && (!_walkable[cell + dx] || !_walkable[cell + dz * _width]))
					continue;
				bestDistance = distance[next];
				best = next;
			}
		}
		return best;
	}

	/// <summary>Dijkstra outwards from the goal over walkable cells.</summary>
	private static void Fill(Field field)
	{
		int[] dist = field.Distance;
		Array.Fill(dist, Unreachable);
		if (field.Goal < 0)
			return;

		float size = 1f / _inverseCell;
		Open.Clear();
		dist[field.Goal] = 0;
		Open.Enqueue(field.Goal, 0);
		while (Open.TryDequeue(out int cell, out int distance))
		{
			if (distance > dist[cell])
				continue;
			for (int dz = -1; dz <= 1; dz++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					if (dx == 0 && dz == 0)
						continue;
					int next = cell + dz * _width + dx;
					if (!_walkable[next])
						continue;
					if (field.AvoidRadius > 0f && field.InAvoid(_originX + (next % _width + 0.5f) * size, _originZ + (next / _width + 0.5f) * size))
						continue;
					bool diagonal = dx != 0 && dz != 0;
					if (diagonal && (!_walkable[cell + dx] || !_walkable[cell + dz * _width]))
						continue;
					int cost = distance + (diagonal ? DiagonalCost : StraightCost);
					if (cost < dist[next])
					{
						dist[next] = cost;
						Open.Enqueue(next, cost);
					}
				}
			}
		}
	}

	/// <summary>True if every cell the segment crosses is walkable.</summary>
	private static bool ClearLine(float x0, float z0, float x1, float z1)
	{
		float gx0 = (x0 - _originX) * _inverseCell, gz0 = (z0 - _originZ) * _inverseCell;
		float gx1 = (x1 - _originX) * _inverseCell, gz1 = (z1 - _originZ) * _inverseCell;
		int cx = Mathf.FloorToInt(gx0), cz = Mathf.FloorToInt(gz0);
		int endX = Mathf.FloorToInt(gx1), endZ = Mathf.FloorToInt(gz1);
		if (cx < 0 || cz < 0 || cx >= _width || cz >= _height || endX < 0 || endZ < 0 || endX >= _width || endZ >= _height)
			return false;

		float dx = gx1 - gx0, dz = gz1 - gz0;
		int stepX = dx > 0f ? 1 : -1, stepZ = dz > 0f ? 1 : -1;
		float tDeltaX = dx != 0f ? Mathf.Abs(1f / dx) : float.MaxValue;
		float tDeltaZ = dz != 0f ? Mathf.Abs(1f / dz) : float.MaxValue;
		float tMaxX = dx != 0f ? (dx > 0f ? cx + 1 - gx0 : gx0 - cx) * tDeltaX : float.MaxValue;
		float tMaxZ = dz != 0f ? (dz > 0f ? cz + 1 - gz0 : gz0 - cz) * tDeltaZ : float.MaxValue;

		int guard = _width + _height;
		while (guard-- > 0)
		{
			if (!_walkable[cz * _width + cx])
				return false;
			if (cx == endX && cz == endZ)
				return true;
			if (tMaxX < tMaxZ)
			{
				tMaxX += tDeltaX;
				cx += stepX;
			}
			else
			{
				tMaxZ += tDeltaZ;
				cz += stepZ;
			}
		}
		return false;
	}

	private static bool InsideConvex(List<float> xs, List<float> zs, float x, float z)
	{
		bool positive = false, negative = false;
		int count = xs.Count;
		for (int i = 0; i < count; i++)
		{
			int j = i + 1 == count ? 0 : i + 1;
			float cross = (xs[j] - xs[i]) * (z - zs[i]) - (zs[j] - zs[i]) * (x - xs[i]);
			if (cross > 0f)
				positive = true;
			else if (cross < 0f)
				negative = true;
			if (positive && negative)
				return false;
		}
		return true;
	}
}
