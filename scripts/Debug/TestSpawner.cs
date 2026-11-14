using Godot;
using Godot.Collections;

namespace Deadset.Debug;

/// <summary>
/// TEMPORARY (until the wave director in Milestone 4): number keys 1-0 spawn a pack of the
/// matching enemy at a random spawn point, so enemy types can be compared side by side.
/// Shows a small key legend in the top-left corner.
/// </summary>
public partial class TestSpawner : Node
{
	/// <summary>One entry per number key, in order 1, 2, ... 9, 0.</summary>
	[Export] public Array<PackedScene> EnemyScenes { get; set; } = new();
	/// <summary>Pack size per entry; missing entries default to 1.</summary>
	[Export] public Array<int> PackSizes { get; set; } = new();
	/// <summary>Index spawned automatically at start (-1 for none).</summary>
	[Export] public int InitialIndex { get; set; } = 0;
	[Export] public float InitialDelay { get; set; } = 1.5f;
	/// <summary>Enemies scatter within this radius around the spawn point.</summary>
	[Export] public float ScatterRadius { get; set; } = 1.6f;

	private static readonly Key[] Keys =
		{ Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9, Key.Key0 };

	public override void _Ready()
	{
		BuildLegend();
		if (InitialIndex >= 0 && InitialIndex < EnemyScenes.Count)
			GetTree().CreateTimer(InitialDelay).Timeout += () => SpawnPack(InitialIndex);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		int index = System.Array.IndexOf(Keys, key.PhysicalKeycode);
		if (index >= 0 && index < EnemyScenes.Count)
			SpawnPack(index);
	}

	private int PackSize(int index) => index < PackSizes.Count ? PackSizes[index] : 1;

	public void SpawnPack(int index)
	{
		var points = GetTree().GetNodesInGroup("enemy_spawn");
		PackedScene scene = EnemyScenes[index];
		if (points.Count == 0 || scene == null)
			return;

		var point = (Node3D)points[GD.RandRange(0, points.Count - 1)];
		Node parent = GetTree().CurrentScene;

		for (int i = 0; i < PackSize(index); i++)
		{
			var enemy = scene.Instantiate<Node3D>();
			float angle = GD.Randf() * Mathf.Tau;
			float radius = Mathf.Sqrt(GD.Randf()) * ScatterRadius;
			Vector3 offset = new(Mathf.Cos(angle) * radius, 0.1f, Mathf.Sin(angle) * radius);
			parent.AddChild(enemy);
			enemy.GlobalPosition = point.GlobalPosition + offset;
		}
	}

	private void BuildLegend()
	{
		var lines = new System.Text.StringBuilder("TEST SPAWN\n");
		for (int i = 0; i < EnemyScenes.Count; i++)
		{
			string name = EnemyScenes[i]?.ResourcePath.GetFile().GetBaseName() ?? "?";
			lines.Append($"{(i + 1) % 10}  {name} x{PackSize(i)}\n");
		}

		var layer = new CanvasLayer { Layer = 5 };
		var label = new Label
		{
			Text = lines.ToString(),
			Position = new Vector2(16, 16),
			Modulate = new Color(1f, 1f, 1f, 0.7f),
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeConstantOverride("outline_size", 4);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		layer.AddChild(label);
		AddChild(layer);
	}
}
