using Deadset.Enemies;
using Godot;
using Godot.Collections;

namespace Deadset.Debug;

/// <summary>
/// DEBUG: keys 1-0 and minus spawn a pack of the
/// matching enemy at a random spawn point, so enemy types can be compared side by side.
/// F1 toggles a small key legend in the top-left corner.
/// F5 spawns a horde (HordeSize enemies, mixed by HordePattern) for performance testing.
/// </summary>
public partial class TestSpawner : Node
{
	/// <summary>One entry per key, in order 1, 2, ... 9, 0, then minus.</summary>
	[Export] public Array<PackedScene> EnemyScenes { get; set; } = new();
	/// <summary>Pack size per entry; missing entries default to 1.</summary>
	[Export] public Array<int> PackSizes { get; set; } = new();
	/// <summary>Index spawned automatically at start (-1 for none).</summary>
	[Export] public int InitialIndex { get; set; } = 0;
	[Export] public float InitialDelay { get; set; } = 1.5f;
	/// <summary>Enemies scatter within this radius around the spawn point.</summary>
	[Export] public float ScatterRadius { get; set; } = 1.6f;

	/// <summary>Show the key legend at start. F1 toggles it.</summary>
	[Export] public bool ShowLegend { get; set; }

	[ExportGroup("Horde (F5)")]
	/// <summary>How many enemies one press of F5 adds.</summary>
	[Export] public int HordeSize { get; set; } = 200;
	/// <summary>
	/// The mix, as indices into EnemyScenes, repeated until HordeSize is reached.
	/// Default (for the list in Main.tscn): 14 emus, 2 wombats, 2 dingoes, a kangaroo and a magpie per 20.
	/// </summary>
	[Export] public Array<int> HordePattern { get; set; } = new() { 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 10, 6, 6, 4, 9 };
	/// <summary>Horde enemies created per physics tick, so the spawn doesn't hitch.</summary>
	[Export] public int HordeSpawnsPerTick { get; set; } = 3;
	/// <summary>Horde enemies scatter within this radius around each spawn point.</summary>
	[Export] public float HordeScatterRadius { get; set; } = 5f;

	private CanvasLayer _legend;
	private Label _hordeLabel;
	private int _hordeQueued;
	private int _hordeIndex;
	private int _shownAlive = -1;
	private int _shownQueued = -1;

	private static readonly Key[] Keys =
		{ Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9, Key.Key0, Key.Minus };

	public override void _Ready()
	{
		BuildLegend();
		SetPhysicsProcess(false);
		if (InitialIndex >= 0 && InitialIndex < EnemyScenes.Count)
			GetTree().CreateTimer(InitialDelay).Timeout += () => SpawnPack(InitialIndex);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		if (key.PhysicalKeycode == Key.F1 && _legend != null)
		{
			_legend.Visible = !_legend.Visible;
			return;
		}
		if (key.PhysicalKeycode == Key.F5)
		{
			SpawnHorde(HordeSize);
			return;
		}
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

	/// <summary>Queues a horde. It trickles in over the next ticks, spread across every spawn point.</summary>
	public void SpawnHorde(int count)
	{
		_hordeQueued += Mathf.Max(count, 0);
		SetPhysicsProcess(true);
	}

	public override void _PhysicsProcess(double delta)
	{
		var points = GetTree().GetNodesInGroup("enemy_spawn");
		if (_hordeQueued <= 0 || points.Count == 0 || EnemyScenes.Count == 0)
		{
			_hordeQueued = 0;
			SetPhysicsProcess(false);
			return;
		}

		Node parent = GetTree().CurrentScene;
		for (int i = Mathf.Max(HordeSpawnsPerTick, 1); i > 0 && _hordeQueued > 0; i--)
		{
			int pattern = HordePattern.Count > 0 ? HordePattern[_hordeIndex % HordePattern.Count] : 0;
			PackedScene scene = EnemyScenes[Mathf.Clamp(pattern, 0, EnemyScenes.Count - 1)];
			var point = (Node3D)points[_hordeIndex % points.Count];
			_hordeIndex++;
			_hordeQueued--;
			if (scene == null)
				continue;

			float angle = GD.Randf() * Mathf.Tau;
			float radius = Mathf.Sqrt(GD.Randf()) * HordeScatterRadius;
			Vector3 position = NavGrid.SnapToGround(
				point.GlobalPosition + new Vector3(Mathf.Cos(angle) * radius, 0.1f, Mathf.Sin(angle) * radius));
			var enemy = scene.Instantiate<Node3D>();
			enemy.Position = position;
			parent.AddChild(enemy);
			enemy.GlobalPosition = position;
		}
	}

	public override void _Process(double delta)
	{
		// Live head count under the legend, rewritten only when it changes.
		if (_hordeLabel == null || !_legend.Visible)
			return;
		int alive = Enemy.AliveCount;
		if (alive == _shownAlive && _hordeQueued == _shownQueued)
			return;
		_shownAlive = alive;
		_shownQueued = _hordeQueued;
		_hordeLabel.Text = _hordeQueued > 0 ? $"alive {alive}  (+{_hordeQueued} arriving)" : $"alive {alive}";
	}

	private void BuildLegend()
	{
		var lines = new System.Text.StringBuilder("TEST SPAWN\n");
		for (int i = 0; i < EnemyScenes.Count; i++)
		{
			string name = EnemyScenes[i]?.ResourcePath.GetFile().GetBaseName() ?? "?";
			string key = i < 10 ? ((i + 1) % 10).ToString() : "-";
			lines.Append($"{key}  {name} x{PackSize(i)}\n");
		}
		lines.Append($"F5  horde x{HordeSize}\n");

		var layer = new CanvasLayer { Layer = 5, Visible = ShowLegend };
		_legend = layer;
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

		_hordeLabel = new Label
		{
			Position = new Vector2(16, 16 + 20 * (EnemyScenes.Count + 3)),
			Modulate = new Color(1f, 1f, 1f, 0.7f),
		};
		_hordeLabel.AddThemeFontSizeOverride("font_size", 14);
		_hordeLabel.AddThemeConstantOverride("outline_size", 4);
		_hordeLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		layer.AddChild(_hordeLabel);
		AddChild(layer);
	}
}
