using System.Collections.Generic;
using System.Threading.Tasks;
using Deadset.Effects;
using Deadset.Enemies;
using Godot;

namespace Deadset.ArtPreviews;

/// <summary>
/// Screenshot tool for the art directions: poses the same shots in every style, saves a PNG of
/// each, measures frame time and builds side-by-side comparison sheets.
/// Run: godot --path . res://art_previews/StyleCapture.tscn -- out=C:/some/folder [styles=0,A,C,E1,E3_05] [shots=1,2,3,4] [perf] [sheets] [hybridsheets]
/// </summary>
public partial class StyleCapture : Node
{
	private static readonly string[] StyleKeys = { "0", "A", "B", "C", "D", "E1", "E2", "E3_0", "E3_05", "E3_1" };
	/// <summary>StyleApplier.Dusk for each entry (only E3 reacts to it).</summary>
	private static readonly float[] StyleDusk = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.5f, 1f };
	private static readonly string[] StyleFiles =
	{
		null,
		"res://art_previews/styles/A_ClassicToon.tres",
		"res://art_previews/styles/B_InkedComic.tres",
		"res://art_previews/styles/C_NeonDusk.tres",
		"res://art_previews/styles/D_OutbackPoster.tres",
		"res://art_previews/styles/E1_PosterNeonThreat.tres",
		"res://art_previews/styles/E2_NeonPosterDusk.tres",
		"res://art_previews/styles/E3_DuskShift.tres",
		"res://art_previews/styles/E3_DuskShift.tres",
		"res://art_previews/styles/E3_DuskShift.tres",
	};
	private static readonly string[] StyleLabels =
	{
		"Current look (baseline)", "A  Classic toon", "B  Inked comic", "C  Neon-mutant dusk", "D  Flat outback poster",
		"E1  Poster + neon threat", "E2  Neon poster dusk", "E3  Dusk shift at 0", "E3  Dusk shift at 0.5", "E3  Dusk shift at 1",
	};
	private static readonly string[] ShotNames = { "", "1_arena", "2_flock", "3_lineup", "4_windup_sparks" };

	private Node3D _main;
	private Node3D _player;
	private StyleApplier _applier;
	private readonly List<Node3D> _enemies = new();
	private string _out = "user://style";

	public override async void _Ready()
	{
		var styles = new List<string> { "0", "A", "B", "C", "D" };
		bool hybridSheets = false;
		var shots = new List<int> { 1, 2, 3, 4 };
		bool perf = false;
		bool sheets = false;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("out="))
				_out = arg[4..];
			else if (arg.StartsWith("styles="))
				styles = new List<string>(arg[7..].Split(','));
			else if (arg.StartsWith("shots="))
			{
				shots.Clear();
				foreach (string s in arg[6..].Split(','))
					if (s.Length > 0)
						shots.Add(int.Parse(s));
			}
			else if (arg == "perf")
				perf = true;
			else if (arg == "sheets")
				sheets = true;
			else if (arg == "hybridsheets")
				hybridSheets = true;
		}
		DirAccess.MakeDirRecursiveAbsolute(_out);

		_main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Node3D>();
		_main.GetNode("TestSpawner").Set("InitialIndex", -1);
		Node director = _main.GetNode("WaveDirector");
		_main.RemoveChild(director);
		director.Free();
		AddChild(_main);

		_player = _main.GetNode<Node3D>("Player");
		_player.SetProcessInput(false);
		_player.SetProcessUnhandledInput(false);
		_player.GetNode("WeaponInput").SetProcessUnhandledInput(false);
		_main.GetNode("TestSpawner").SetProcessUnhandledInput(false);
		Input.MouseMode = Input.MouseModeEnum.Visible;

		_applier = new StyleApplier { Name = "StyleApplier" };
		_main.AddChild(_applier);

		await Frames(30);

		var perfLines = new List<string>();
		foreach (int shot in shots)
		{
			if (shot != 4)
				SetUpShot(shot);
			foreach (string key in styles)
			{
				int index = System.Array.IndexOf(StyleKeys, key);
				if (index < 0)
					continue;
				_applier.Dusk = StyleDusk[index];
				_applier.Apply(StyleFiles[index] == null ? null : GD.Load<CelStyle>(StyleFiles[index]));
				string path = $"{_out}/{ShotNames[shot]}_{key}.png";
				if (shot == 4)
					await WindupShot(path);
				else
				{
					await Frames(40);
					await Save(path);
					if (shot == 1)
						SaveZoom(path, new Rect2I(640, 400, 320, 130), 5);
				}
				if (perf && shot == 2)
					perfLines.Add($"{StyleLabels[index]}: {await Measure()}");
			}
		}
		_applier.Apply(null);

		if (perfLines.Count > 0)
		{
			using var file = FileAccess.Open($"{_out}/perf.txt", FileAccess.ModeFlags.Write);
			foreach (string line in perfLines)
			{
				file.StoreLine(line);
				GD.Print(line);
			}
		}

		if (sheets)
			for (int shot = 1; shot <= 3; shot++)
				await BuildSheet($"sheet_{ShotNames[shot]}", shot, new[] { "0", "A", "B", "C", "D" }, 3);
		if (hybridSheets)
		{
			for (int shot = 1; shot <= 3; shot++)
				await BuildSheet($"sheet_hybrids_{ShotNames[shot]}", shot, new[] { "C", "D", "E1", "E2", "E3_05" }, 3);
			await BuildSheet("sheet_E3_progression_2_flock", 2, new[] { "E3_0", "E3_05", "E3_1" }, 3);
		}

		GetTree().Quit();
	}

	// ---- Shots --------------------------------------------------------------------------

	private void SetUpShot(int shot)
	{
		foreach (Node3D enemy in _enemies)
			if (IsInstanceValid(enemy))
				enemy.Free();
		_enemies.Clear();

		switch (shot)
		{
			case 1:
				// Arena overview from the south spawn, cover and sky in view, two emus at range.
				Pose(new Vector3(-2f, 0.1f, 16f), 0f, 0.02f);
				Place("Emu", new Vector3(-2.5f, 0f, -2.5f));
				Place("Emu", new Vector3(1.5f, 0f, -8f));
				break;
			case 2:
				// Flock at 8-15 m, head height.
				Pose(new Vector3(0f, 0.1f, 4f), 0f, 0f);
				Place("Emu", new Vector3(-2.6f, 0f, -4.2f));
				Place("Emu", new Vector3(0.6f, 0f, -5.2f));
				Place("Emu", new Vector3(3.2f, 0f, -6.6f));
				Place("Emu", new Vector3(-1.4f, 0f, -8.4f));
				Place("Emu", new Vector3(1.8f, 0f, -10f));
				Place("Emu", new Vector3(-4.2f, 0f, -11f));
				break;
			case 3:
				// Close line-up for silhouettes, weapon in frame.
				Pose(new Vector3(0f, 0.1f, 3f), 0f, -0.1f);
				Place("Wombat", new Vector3(-2.3f, 0f, -0.6f), 0.6f);
				Place("Emu", new Vector3(-0.5f, 0f, -1.2f), -0.5f);
				Place("Kangaroo", new Vector3(1.2f, 0f, -0.8f), -0.45f);
				break;
		}
	}

	/// <summary>A live wombat in mid wind-up (glow rising) plus two spark bursts.</summary>
	private async Task WindupShot(string path)
	{
		SetUpShot(0);
		Pose(new Vector3(0f, 0.1f, 4f), 0f, -0.05f);
		var wombat = (Wombat)Place("Wombat", new Vector3(-0.8f, 0f, -8.5f), 0f, frozen: false);
		Place("Emu", new Vector3(2.6f, 0f, -9f));

		for (int i = 0; i < 400 && wombat.StateName != "Windup"; i++)
			await Frames(1);
		// Hold it at the end of the wind-up (direction locked, glow at full) so every style
		// is caught at the same moment and it never gets to charge.
		await ToSignal(GetTree().CreateTimer(0.64), SceneTreeTimer.SignalName.Timeout);
		wombat.SetPhysicsProcess(false);
		await Frames(20);
		ImpactEffects.SpawnSparks(_main, new Vector3(1.6f, 1.2f, 0.5f), Vector3.Up, new Color(1f, 0.85f, 0.5f), 24);
		ImpactEffects.SpawnSparks(_main, new Vector3(2.6f, 1.3f, -5f), Vector3.Up, new Color(0.45f, 1f, 0.4f), 30);
		await ToSignal(GetTree().CreateTimer(0.09), SceneTreeTimer.SignalName.Timeout);
		await Save(path);
		SetUpShot(0);
		await Frames(25);
	}

	private void Pose(Vector3 position, float yaw, float pitch)
	{
		_player.GlobalPosition = position;
		_player.Rotation = new Vector3(0f, yaw, 0f);
		_player.GetNode<Node3D>("Head").Rotation = new Vector3(pitch, 0f, 0f);
		if (_player is CharacterBody3D body)
			body.Velocity = Vector3.Zero;
	}

	private Node3D Place(string type, Vector3 position, float turn = 0f, bool frozen = true)
	{
		var enemy = GD.Load<PackedScene>($"res://scenes/enemies/{type}.tscn").Instantiate<Node3D>();
		_main.AddChild(enemy);
		enemy.GlobalPosition = position;
		// Models face -Z. Turn to face the player, plus an optional extra turn.
		Vector3 to = _player.GlobalPosition - position;
		enemy.GetNode<Node3D>("Visual").Rotation = new Vector3(0f, Mathf.Atan2(-to.X, -to.Z) + turn, 0f);
		if (frozen)
			enemy.SetPhysicsProcess(false);
		_enemies.Add(enemy);
		return enemy;
	}

	// ---- Capture and measurement --------------------------------------------------------

	private async Task Frames(int count)
	{
		for (int i = 0; i < count; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async Task Save(string path)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServerInstance.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("Saved ", path);
	}

	/// <summary>Enlarged crop of a saved shot, to judge how targets read at range.</summary>
	private static void SaveZoom(string path, Rect2I region, int scale)
	{
		Image crop = Image.LoadFromFile(path).GetRegion(region);
		crop.Resize(region.Size.X * scale, region.Size.Y * scale, Image.Interpolation.Nearest);
		crop.SavePng(path.Replace(".png", "_zoom.png"));
	}

	private async Task<string> Measure()
	{
		Rid viewport = GetViewport().GetViewportRid();
		DisplayServer.VSyncMode previous = DisplayServer.WindowGetVsyncMode();
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
		await Frames(30);

		const int samples = 240;
		double gpu = 0.0;
		double cpu = 0.0;
		ulong start = Time.GetTicksUsec();
		for (int i = 0; i < samples; i++)
		{
			await Frames(1);
			gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
			cpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport);
		}
		double frameMs = (Time.GetTicksUsec() - start) / 1000.0 / samples;
		RenderingServer.ViewportSetMeasureRenderTime(viewport, false);
		DisplayServer.WindowSetVsyncMode(previous);
		return $"frame {frameMs:0.00} ms ({1000.0 / frameMs:0} fps uncapped), viewport GPU {gpu / samples:0.00} ms, render CPU {cpu / samples:0.00} ms";
	}

	// ---- Comparison sheets --------------------------------------------------------------

	/// <summary>One PNG for a shot type: the given styles in a labelled grid.</summary>
	private async Task BuildSheet(string name, int shot, string[] keys, int columns)
	{
		const int tileW = 960, tileH = 540, labelH = 46, pad = 10;
		int rows = (keys.Length + columns - 1) / columns;
		int cellH = tileH + labelH;
		var sheet = Image.CreateEmpty(columns * tileW + (columns + 1) * pad, rows * cellH + (rows + 1) * pad, false, Image.Format.Rgba8);
		sheet.Fill(new Color(0.09f, 0.08f, 0.08f));

		for (int i = 0; i < keys.Length; i++)
		{
			string path = $"{_out}/{ShotNames[shot]}_{keys[i]}.png";
			if (!FileAccess.FileExists(path))
				continue;
			Image tile = Image.LoadFromFile(path);
			tile.Convert(Image.Format.Rgba8);
			tile.Resize(tileW, tileH, Image.Interpolation.Lanczos);
			int x = pad + (i % columns) * (tileW + pad);
			int y = pad + (i / columns) * (cellH + pad);
			Image label = await RenderLabel(StyleLabels[System.Array.IndexOf(StyleKeys, keys[i])], tileW, labelH);
			sheet.BlitRect(label, new Rect2I(0, 0, tileW, labelH), new Vector2I(x, y));
			sheet.BlitRect(tile, new Rect2I(0, 0, tileW, tileH), new Vector2I(x, y + labelH));
		}
		string outPath = $"{_out}/{name}.png";
		sheet.SavePng(outPath);
		GD.Print("Saved ", outPath);
	}

	private async Task<Image> RenderLabel(string text, int width, int height)
	{
		var viewport = new SubViewport
		{
			Size = new Vector2I(width, height),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			TransparentBg = false,
		};
		var background = new ColorRect { Color = new Color(0.16f, 0.14f, 0.13f), Size = new Vector2(width, height) };
		var label = new Label
		{
			Text = text,
			Size = new Vector2(width, height),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 28);
		viewport.AddChild(background);
		viewport.AddChild(label);
		AddChild(viewport);
		await Frames(2);
		await ToSignal(RenderingServer.Singleton, RenderingServerInstance.SignalName.FramePostDraw);
		Image image = viewport.GetTexture().GetImage();
		image.Convert(Image.Format.Rgba8);
		viewport.QueueFree();
		return image;
	}
}
