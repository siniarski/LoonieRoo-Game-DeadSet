using Deadset.Core;
using Godot;

namespace Deadset.UI;

/// <summary>
/// Shown when the run ends: a short slow-motion beat, then the run summary and a restart.
/// Display only; the numbers come from RunStats.
/// </summary>
public partial class GameOverScreen : CanvasLayer
{
	/// <summary>Game speed during the death beat.</summary>
	[Export(PropertyHint.Range, "0.05,1")] public float DeathSlowMo { get; set; } = 0.3f;
	/// <summary>Real seconds of slow motion before the summary appears.</summary>
	[Export] public float DelayBeforeSummary { get; set; } = 1.4f;
	[Export] public Color Accent { get; set; } = new(1f, 0.55f, 0.2f);

	private RunStats _stats;
	private Control _root;
	private Label _summary;
	private bool _shown;

	public override void _Ready()
	{
		Layer = 20;
		ProcessMode = ProcessModeEnum.Always;
		BuildUi();
		_root.Visible = false;
		CallDeferred(MethodName.Bind);
	}

	public override void _ExitTree()
	{
		// Never leave the next run in slow motion.
		Engine.TimeScale = 1.0;
	}

	private void Bind()
	{
		_stats = GetTree().GetFirstNodeInGroup("run_stats") as RunStats;
		if (_stats != null)
			_stats.RunEnded += OnRunEnded;
	}

	private void OnRunEnded()
	{
		Engine.TimeScale = DeathSlowMo;
		GetTree().CreateTimer(DelayBeforeSummary, processAlways: true, processInPhysics: false, ignoreTimeScale: true)
			.Timeout += Show;
	}

	private new void Show()
	{
		Engine.TimeScale = 1.0;
		_shown = true;
		int minutes = Mathf.FloorToInt(_stats.Seconds / 60f);
		int seconds = Mathf.FloorToInt(_stats.Seconds % 60f);
		_summary.Text = $"Reached wave {_stats.HighestWave}   ·   {_stats.WavesCleared} cleared\n"
			+ $"{_stats.Kills} kills   ·   {minutes}:{seconds:00} survived";
		_root.Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_shown)
			return;
		if (@event.IsActionPressed("reload") || @event.IsActionPressed("ready_up"))
		{
			GetViewport().SetInputAsHandled();
			Restart();
		}
	}

	private void Restart()
	{
		Engine.TimeScale = 1.0;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_stats.Restart();
	}

	private void BuildUi()
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0.05f, 0f, 0f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		column.AddThemeConstantOverride("separation", 22);
		_root.AddChild(column);

		var title = MakeLabel("YOU CARKED IT", 84, new Color(1f, 0.3f, 0.25f));
		column.AddChild(title);
		_summary = MakeLabel("", 30, Colors.White);
		column.AddChild(_summary);

		var button = new Button
		{
			Text = "HAVE ANOTHER GO   [R]",
			CustomMinimumSize = new Vector2(420, 70),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			FocusMode = Control.FocusModeEnum.None,
		};
		button.AddThemeFontSizeOverride("font_size", 28);
		var normal = new StyleBoxFlat { BgColor = new Color(0.16f, 0.1f, 0.07f), BorderColor = Accent };
		normal.SetBorderWidthAll(3);
		normal.SetCornerRadiusAll(6);
		var hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = new Color(0.3f, 0.17f, 0.09f);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", hover);
		button.Pressed += Restart;
		column.AddChild(button);
	}

	private static Label MakeLabel(string text, int size, Color color)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 10);
		return label;
	}
}
