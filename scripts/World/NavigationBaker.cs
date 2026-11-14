using Godot;

namespace Deadset.World;

/// <summary>
/// Bakes the arena's navigation mesh at startup from the geometry under this node,
/// so editing the arena never leaves a stale navmesh behind.
/// </summary>
public partial class NavigationBaker : NavigationRegion3D
{
	public override async void _Ready()
	{
		// CSG shapes build their meshes a frame after entering the tree.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		BakeNavigationMesh(onThread: false);
		GD.Print($"Navigation baked: {NavigationMesh.GetPolygonCount()} polygons.");
	}
}
