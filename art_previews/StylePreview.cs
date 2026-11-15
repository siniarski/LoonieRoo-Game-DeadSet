using Deadset.Visuals;
using Deadset.Debug;
using Godot;
using Godot.Collections;

namespace Deadset.ArtPreviews;

/// <summary>
/// The real game with switchable art directions: 1-9 pick a direction, 0 returns to the current look.
/// Minus and equals step the dusk shift of a style that has one (E3, F1, F2).
/// Nothing in the game scenes is changed; StyleApplier restyles the running tree.
/// F2 / F3 / F4 spawn an emu pack, a wombat and kangaroos, to look at them without waiting for a wave.
/// Optional test run: godot --path . res://art_previews/StylePreview.tscn -- style=2 shot=C:/out.png delay=10
/// </summary>
public partial class StylePreview : Node
{
	/// <summary>One per key, in order 1, 2, 3, ...</summary>
	[Export] public Array<CelStyle> Styles { get; set; } = new();

	// Survives "have another go", which reloads this scene.
	private static int _selected;
	private static float _dusk;

	private StyleApplier _applier;
	private TestSpawner _spawner;
	private Label _label;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		var main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Node3D>();
		// The game now carries its own style; previews swap styles themselves.
		main.GetNodeOrNull("StyleApplier")?.Free();
		main.GetNodeOrNull("DuskByWave")?.Free();
		_spawner = main.GetNode<TestSpawner>("TestSpawner");
		_spawner.InitialIndex = -1;
		AddChild(main);
		// Its number keys would clash with the style keys.
		_spawner.SetProcessUnhandledInput(false);

		// Enemies and nails are added to the current scene (this node), so restyle from here down.
		_applier = new StyleApplier { Name = "StyleApplier" };
		AddChild(_applier);

		var layer = new CanvasLayer { Layer = 20 };
		_label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			OffsetTop = 12f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_label.AddThemeFontSizeOverride("font_size", 20);
		_label.AddThemeColorOverride("font_outline_color", Colors.Black);
		_label.AddThemeConstantOverride("outline_size", 8);
		layer.AddChild(_label);
		AddChild(layer);

		string shot = null;
		double delay = 8.0;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("style="))
				_selected = int.Parse(arg[6..]);
			else if (arg.StartsWith("dusk="))
				_dusk = float.Parse(arg[5..], System.Globalization.CultureInfo.InvariantCulture);
			else if (arg.StartsWith("shot="))
				shot = arg[5..];
			else if (arg.StartsWith("delay="))
				delay = double.Parse(arg[6..], System.Globalization.CultureInfo.InvariantCulture);
		}

		Select(_selected);
		if (shot != null)
			TestShot(shot, delay);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		switch (key.PhysicalKeycode)
		{
			case Key.Key0: Select(0); break;
			case Key.Key1: Select(1); break;
			case Key.Key2: Select(2); break;
			case Key.Key3: Select(3); break;
			case Key.Key4: Select(4); break;
			case Key.Key5: Select(5); break;
			case Key.Key6: Select(6); break;
			case Key.Key7: Select(7); break;
			case Key.Key8: Select(8); break;
			case Key.Key9: Select(9); break;
			case Key.Minus: _dusk = Mathf.Max(_dusk - 0.1f, 0f); Select(_selected); break;
			case Key.Equal: _dusk = Mathf.Min(_dusk + 0.1f, 1f); Select(_selected); break;
			case Key.F2: _spawner.SpawnPack(0); break;    // emus
			case Key.F3: _spawner.SpawnPack(10); break;   // wombat
			case Key.F4: _spawner.SpawnPack(4); break;    // kangaroos
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}

	private void Select(int index)
	{
		_selected = Mathf.Clamp(index, 0, Styles.Count);
		CelStyle style = _selected == 0 ? null : Styles[_selected - 1];
		_applier.Dusk = _dusk;
		_applier.Apply(style);
		string name = style == null ? "0  Current look" : style.DisplayName;
		if (style?.DuskTarget != null)
			name += $"   dusk {_dusk:0.0}  [-] [=]";
		_label.Text = $"{name}\n[1-9] art directions   [0] current look   [F2/F3/F4] spawn emus / wombat / kangaroos";
	}

	/// <summary>Lets the game run for a while, saves one screenshot and quits. Leaves the mouse alone.</summary>
	private async void TestShot(string path, double delay)
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
		await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
		_spawner.SpawnPack(0);
		_spawner.SpawnPack(10);
		await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
		await ToSignal(RenderingServer.Singleton, RenderingServerInstance.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GetTree().Quit();
	}
}
