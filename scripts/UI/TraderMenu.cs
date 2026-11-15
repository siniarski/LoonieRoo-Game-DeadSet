using System.Collections.Generic;
using Deadset.Core;
using Deadset.Economy;
using Deadset.Upgrades;
using Deadset.Waves;
using Godot;

namespace Deadset.UI;

/// <summary>
/// Display for the Trader: shop buttons and the 1-of-3 upgrade cards.
/// Opens automatically when a break starts; B reopens it during the break,
/// Esc/B closes it, Enter readies up. All rules and state live in Trader.
/// </summary>
public partial class TraderMenu : CanvasLayer
{
	[Export] public Color Accent { get; set; } = new(1f, 0.55f, 0.2f);
	[Export] public Color PanelColor { get; set; } = new(0.09f, 0.07f, 0.06f, 1f);
	[Export] public Key ToggleKey { get; set; } = Key.B;

	private Trader _trader;
	private Control _root;
	private Label _title;
	private Label _status;
	private Label _summary;
	private Label _cash;
	private Button _fillNails;
	private Button _buyMagazine;
	private Button _buyArmour;
	private Button _fillArmour;
	private Label _pickHeader;
	private readonly List<Button> _cards = new();
	private HBoxContainer _cardRow;

	public override void _Ready()
	{
		Layer = 10;
		BuildUi();
		_root.Visible = false;
		CallDeferred(MethodName.Bind);
	}

	private void Bind()
	{
		_trader = GetTree().GetFirstNodeInGroup("trader") as Trader;
		if (_trader == null)
		{
			GD.PushWarning("TraderMenu: no Trader in the scene.");
			return;
		}
		_trader.Opened += () => { RebuildCards(); SetOpen(true); };
		_trader.Closed += () => SetOpen(false);
		_trader.StockChanged += Refresh;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_trader == null || !_trader.IsOpen)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false } key && key.PhysicalKeycode == ToggleKey)
		{
			SetOpen(!_root.Visible);
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ready_up") && _root.Visible)
		{
			// Leave the event unhandled so the WaveDirector also sees the ready-up.
			SetOpen(false);
		}
		else if (@event.IsActionPressed("release_mouse") && _root.Visible)
		{
			SetOpen(false);
			GetViewport().SetInputAsHandled();
		}
	}

	private void SetOpen(bool open)
	{
		_root.Visible = open;
		Input.MouseMode = open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		if (open)
			Refresh();
	}

	// ---- Layout ----

	private void BuildUi()
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", Box(PanelColor, Accent, 3, 18));
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.OffsetLeft = -590; panel.OffsetRight = 590;
		panel.OffsetTop = -300; panel.OffsetBottom = 300;
		_root.AddChild(panel);

		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 14);
		panel.AddChild(outer);

		var header = new HBoxContainer();
		outer.AddChild(header);
		_title = MakeLabel("TRADER", 40, Accent);
		_title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(_title);
		_cash = MakeLabel("$0", 40, new Color(0.55f, 1f, 0.45f));
		header.AddChild(_cash);

		_summary = MakeLabel("", 20, new Color(1f, 0.85f, 0.6f));
		outer.AddChild(_summary);

		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 28);
		body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		outer.AddChild(body);

		// Shop column.
		var shop = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
		shop.AddThemeConstantOverride("separation", 10);
		body.AddChild(shop);
		shop.AddChild(MakeLabel("SUPPLIES", 22, new Color(1f, 1f, 1f, 0.7f)));
		_fillNails = MakeShopButton(shop, () => _trader.BuyNails(_trader.AmmoSpace));
		_buyMagazine = MakeShopButton(shop, () => _trader.BuyNails(_trader.MagazineSize));
		_buyArmour = MakeShopButton(shop, () => _trader.BuyArmour(_trader.ArmourStep));
		_fillArmour = MakeShopButton(shop, () => _trader.BuyArmour(_trader.ArmourSpace));
		_status = MakeLabel("", 18, new Color(1f, 1f, 1f, 0.75f));
		shop.AddChild(_status);

		// Upgrade column.
		var picks = new VBoxContainer();
		picks.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		picks.AddThemeConstantOverride("separation", 10);
		body.AddChild(picks);
		_pickHeader = MakeLabel("PICK ONE UPGRADE", 22, new Color(1f, 1f, 1f, 0.7f));
		picks.AddChild(_pickHeader);
		_cardRow = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		_cardRow.AddThemeConstantOverride("separation", 14);
		picks.AddChild(_cardRow);

		var footer = MakeLabel("[ENTER] READY UP      [B] CLOSE / REOPEN DURING BREAK", 18, new Color(1f, 1f, 1f, 0.6f));
		footer.HorizontalAlignment = HorizontalAlignment.Center;
		outer.AddChild(footer);
	}

	private void RebuildCards()
	{
		foreach (Button card in _cards)
			card.QueueFree();
		_cards.Clear();

		for (int i = 0; i < _trader.Choices.Count; i++)
		{
			UpgradeDefinition upgrade = _trader.Choices[i];
			int index = i;
			var card = new Button
			{
				CustomMinimumSize = new Vector2(230, 210),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
				FocusMode = Control.FocusModeEnum.None,
				ClipText = false,
			};
			card.AddThemeStyleboxOverride("normal", Box(new Color(0.16f, 0.13f, 0.11f), upgrade.AccentColor, 3, 12));
			card.AddThemeStyleboxOverride("hover", Box(new Color(0.24f, 0.19f, 0.15f), upgrade.AccentColor, 5, 12));
			card.AddThemeStyleboxOverride("pressed", Box(new Color(0.3f, 0.24f, 0.18f), upgrade.AccentColor, 6, 12));
			card.AddThemeStyleboxOverride("disabled", Box(new Color(0.1f, 0.09f, 0.08f), upgrade.AccentColor.Darkened(0.6f), 2, 12));
			card.Pressed += () => _trader.PickUpgrade(index);

			var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			content.OffsetLeft = 16; content.OffsetRight = -16; content.OffsetTop = 18; content.OffsetBottom = -16;
			content.AddThemeConstantOverride("separation", 12);
			card.AddChild(content);

			var swatch = new ColorRect { Color = upgrade.AccentColor, CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
			content.AddChild(swatch);
			var name = MakeLabel(upgrade.DisplayName, 26, upgrade.AccentColor);
			name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			content.AddChild(name);
			var description = MakeLabel(upgrade.Description, 18, new Color(1f, 1f, 1f, 0.85f));
			description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			content.AddChild(description);
			int owned = _trader.Upgrades?.StacksOf(upgrade) ?? 0;
			if (upgrade.MaxStacks > 1 || owned > 0)
				content.AddChild(MakeLabel($"Owned {owned} / {upgrade.MaxStacks}", 16, new Color(1f, 1f, 1f, 0.5f)));

			_cardRow.AddChild(card);
			_cards.Add(card);
		}

		if (_trader.Choices.Count == 0)
		{
			var none = MakeLabel("No upgrades left to offer.", 20, new Color(1f, 1f, 1f, 0.6f));
			_cardRow.AddChild(none);
		}
	}

	private void Refresh()
	{
		if (_trader == null || !_root.Visible)
			return;

		int cash = _trader.Wallet?.Cash ?? 0;
		_cash.Text = $"${cash}";

		if (GetTree().GetFirstNodeInGroup("run_stats") is RunStats stats && stats.LastWave.Wave > 0)
		{
			RunStats.WaveSummary w = stats.LastWave;
			_summary.Text = $"WAVE {w.Wave} DONE   ·   {w.Kills} kills   ·   {Mathf.RoundToInt(w.HeadshotPercent)}% headshots   ·   +${w.CashEarned}   ·   "
				+ $"{Mathf.FloorToInt(w.Seconds / 60f)}:{Mathf.FloorToInt(w.Seconds % 60f):00}   ·   took {Mathf.RoundToInt(w.DamageTaken)} dmg";
		}
		else
		{
			_summary.Text = "";
		}

		var director = GetTree().GetFirstNodeInGroup("wave_director") as WaveDirector;
		_title.Text = director != null ? $"TRADER  ·  WAVE {director.WaveNumber} NEXT" : "TRADER";

		int space = _trader.AmmoSpace;
		int mag = Mathf.Min(_trader.MagazineSize, space);
		SetShopButton(_fillNails, $"Fill nails  (+{space})", _trader.CostOfNails(space), space > 0, cash);
		SetShopButton(_buyMagazine, $"One magazine  (+{mag})", _trader.CostOfNails(mag), mag > 0, cash);
		int armourSpace = _trader.ArmourSpace;
		int step = Mathf.Min(_trader.ArmourStep, armourSpace);
		SetShopButton(_buyArmour, $"Armour  (+{step})", _trader.CostOfArmour(step), step > 0, cash);
		SetShopButton(_fillArmour, $"Fill armour  (+{armourSpace})", _trader.CostOfArmour(armourSpace), armourSpace > 0, cash);

		var weapon = _trader.Weapon;
		var health = _trader.Health;
		_status.Text = $"Nails  {weapon?.AmmoInMagazine ?? 0} + {weapon?.ReserveAmmo ?? 0} / {weapon?.Stats.MaxReserve ?? 0}\n"
			+ $"Armour  {Mathf.RoundToInt(health?.Armour ?? 0)} / {Mathf.RoundToInt(health?.MaxArmour ?? 0)}";

		bool picked = _trader.PickedIndex >= 0;
		_pickHeader.Text = picked ? "UPGRADE TAKEN" : "PICK ONE UPGRADE";
		for (int i = 0; i < _cards.Count; i++)
		{
			_cards[i].Disabled = picked;
			_cards[i].Modulate = picked && i != _trader.PickedIndex ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
		}
	}

	private static void SetShopButton(Button button, string text, int cost, bool available, int cash)
	{
		button.Text = available ? $"{text}    ${cost}" : $"{text}    FULL";
		bool affordable = cost <= cash || (available && cash > 0);
		button.Disabled = !available || cash <= 0 || !affordable;
	}

	private Button MakeShopButton(Container parent, System.Action onPressed)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(0, 52),
			FocusMode = Control.FocusModeEnum.None,
			Alignment = HorizontalAlignment.Left,
		};
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeStyleboxOverride("normal", Box(new Color(0.18f, 0.14f, 0.11f), Accent.Darkened(0.3f), 2, 8));
		button.AddThemeStyleboxOverride("hover", Box(new Color(0.26f, 0.19f, 0.14f), Accent, 3, 8));
		button.AddThemeStyleboxOverride("pressed", Box(new Color(0.34f, 0.24f, 0.16f), Accent, 3, 8));
		button.AddThemeStyleboxOverride("disabled", Box(new Color(0.12f, 0.1f, 0.09f), new Color(0.3f, 0.3f, 0.3f), 1, 8));
		button.Pressed += onPressed;
		parent.AddChild(button);
		return button;
	}

	private static Label MakeLabel(string text, int size, Color color)
	{
		var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static StyleBoxFlat Box(Color background, Color border, int borderWidth, int padding)
	{
		var box = new StyleBoxFlat { BgColor = background, BorderColor = border };
		box.SetBorderWidthAll(borderWidth);
		box.SetCornerRadiusAll(6);
		box.SetContentMarginAll(padding);
		return box;
	}
}
