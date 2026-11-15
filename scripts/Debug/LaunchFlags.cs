using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Economy;
using Deadset.Enemies;
using Deadset.Player;
using Deadset.Upgrades;
using Deadset.Waves;
using Deadset.Weapons;
using Godot;

namespace Deadset.Debug;

/// <summary>
/// Jump-in flags for quick playtests. Pass them after "--" on the Godot command line, or use
/// play.cmd in the project root, which does that for you:
///
///   play.cmd --wave=5 --weapon=rivet --cash=800
///
/// --wave=N           start at wave N (short countdown)
/// --trader           open the trader break before that wave instead
/// --cash=N           start with N cash
/// --weapon=NAME      own a weapon (nail, cutter, rivet); repeatable, the last one is in your hands
/// --mod=ID[:LEVELS]  give a weapon mod (ids in resources/weapons/mods), on whichever weapon has it
/// --upgrade=ID[:N]   give a player upgrade (ids in resources/upgrades)
/// --spawn=ENEMY:N    N enemies in a ring around you (emu, wombat, dingo, ...); repeatable
/// --at=SPOT          start at a named spot in the arena (alley, shed, tank, north, south)
/// --god              can't die
/// --nowaves          no wave director: an empty arena for --spawn tests
/// --sim=LOADOUT      a simulated player plays the run and logs "SIM" lines (nail, rivet, cutter)
/// --simspeed=N       game speed for --sim runs (default 1)
/// --simwaves=N       --sim quits after this wave (default 10)
///
/// Unknown flags are reported in the console and ignored.
/// </summary>
public partial class LaunchFlags : Node
{
	/// <summary>Radius of the --spawn ring, in metres.</summary>
	[Export] public float SpawnRingRadius { get; set; } = 8f;
	/// <summary>Countdown before the --wave wave, in seconds.</summary>
	[Export] public float JumpCountdown { get; set; } = 3f;

	public override void _Ready()
	{
		string[] args = OS.GetCmdlineUserArgs();
		if (args.Length == 0)
			return;
		// Everything else in the scene sets itself up in _Ready and deferred calls; go after that.
		CallDeferred(MethodName.ApplyAfterSetup, args);
	}

	private async void ApplyAfterSetup(string[] args)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		var arsenal = player?.GetNodeOrNull<PlayerWeaponInput>("WeaponInput");
		var trader = GetTree().GetFirstNodeInGroup("trader") as Trader;
		var director = GetTree().GetFirstNodeInGroup("wave_director") as WaveDirector;
		var spawns = new List<(string enemy, int count)>();
		int wave = 0;
		string simLoadout = null;
		int simWaves = 10;
		bool openTrader = false;

		foreach (string raw in args)
		{
			string arg = raw.TrimStart('-');
			string key = arg.Split('=')[0].ToLowerInvariant();
			string value = arg.Contains('=') ? arg[(arg.IndexOf('=') + 1)..] : "";
			switch (key)
			{
				case "wave":
					wave = Mathf.Max(1, value.ToInt());
					break;
				case "trader":
					openTrader = true;
					break;
				case "cash":
					Wallet.FindOn(player)?.Add(value.ToInt());
					break;
				case "weapon":
					GiveWeapon(trader, arsenal, value);
					break;
				case "mod":
					GiveMod(arsenal, value);
					break;
				case "upgrade":
					GiveUpgrade(player, value);
					break;
				case "spawn":
					string[] parts = value.Split(':');
					spawns.Add((parts[0].ToLowerInvariant(), parts.Length > 1 ? Mathf.Max(1, parts[1].ToInt()) : 8));
					break;
				case "at":
					MovePlayer(player, value);
					break;
				case "god":
					if (HealthComponent.FindOn(player) is { } health)
						health.AddDamageModifier(_ => 0f);
					break;
				case "sim":
					simLoadout = string.IsNullOrEmpty(value) ? "nail" : value.ToLowerInvariant();
					break;
				case "simspeed":
					Engine.TimeScale = Mathf.Clamp(value.ToFloat(), 0.1f, 8f);
					break;
				case "simwaves":
					simWaves = Mathf.Max(1, value.ToInt());
					break;
				case "nowaves":
					director?.Stop();
					director?.QueueFree();
					director = null;
					break;
				default:
					GD.PushWarning($"LaunchFlags: unknown flag '{raw}'.");
					break;
			}
		}

		if (simLoadout != null)
			AddChild(new SimPlayer { Name = "SimPlayer", Loadout = simLoadout, MaxWaves = simWaves });

		if (director != null && (wave > 0 || openTrader))
			director.JumpTo(Mathf.Max(wave, 1), openTrader, JumpCountdown);

		if (spawns.Count > 0)
		{
			// Wait for the navigation grid so enemies land on walkable ground.
			while (!NavGrid.Built)
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			foreach ((string enemy, int count) in spawns)
				SpawnRing(player, enemy, count);
		}
		GD.Print($"LaunchFlags: applied {string.Join(' ', args)}");
	}

	private static string Simplify(string text) => text.ToLowerInvariant().Replace(" ", "").Replace("_", "");

	private static void GiveWeapon(Trader trader, PlayerWeaponInput arsenal, string name)
	{
		if (trader == null || arsenal == null)
			return;
		string wanted = Simplify(name);
		if (wanted is "nail" or "nailgun")
		{
			arsenal.EquipSlot(0);
			return;
		}
		foreach (Trader.WeaponOffer offer in trader.Offers)
		{
			string have = Simplify(offer.Stats.DisplayName);
			if (!have.StartsWith(wanted) && !have.Contains(wanted))
				continue;
			Weapon owned = arsenal.FindByStats(offer.Stats);
			if (owned != null)
				arsenal.Equip(owned);
			else
				arsenal.Give(offer.Scene);
			return;
		}
		GD.PushWarning($"LaunchFlags: no weapon called '{name}'.");
	}

	private static void GiveMod(PlayerWeaponInput arsenal, string value)
	{
		if (arsenal == null)
			return;
		string[] parts = value.Split(':');
		int levels = parts.Length > 1 ? Mathf.Max(1, parts[1].ToInt()) : 1;
		foreach (Weapon weapon in arsenal.Weapons)
		{
			foreach (WeaponMod mod in weapon.Stats.Mods)
			{
				if (mod?.Id != parts[0])
					continue;
				for (int i = 0; i < levels; i++)
					weapon.AddModLevel(mod);
				return;
			}
		}
		GD.PushWarning($"LaunchFlags: no owned weapon has mod '{parts[0]}' (give the weapon first).");
	}

	private static void GiveUpgrade(PlayerController player, string value)
	{
		var upgrades = player?.GetNodeOrNull<PlayerUpgrades>("Upgrades");
		if (upgrades?.Pool == null)
			return;
		string[] parts = value.Split(':');
		int stacks = parts.Length > 1 ? Mathf.Max(1, parts[1].ToInt()) : 1;
		foreach (UpgradeDefinition upgrade in upgrades.Pool.Upgrades)
		{
			if (upgrade?.Id != parts[0])
				continue;
			for (int i = 0; i < stacks; i++)
				if (upgrades.CanGrant(upgrade))
					upgrades.Grant(upgrade);
			return;
		}
		GD.PushWarning($"LaunchFlags: no upgrade with id '{parts[0]}'.");
	}

	private void MovePlayer(PlayerController player, string spot)
	{
		foreach (Node node in GetTree().GetNodesInGroup("player_spot"))
		{
			if (node is not Node3D marker || !Simplify(marker.Name).Contains(Simplify(spot)))
				continue;
			player.GlobalPosition = marker.GlobalPosition + Vector3.Up * 0.1f;
			player.Rotation = new Vector3(0f, marker.GlobalRotation.Y, 0f);
			return;
		}
		GD.PushWarning($"LaunchFlags: no arena spot called '{spot}'.");
	}

	private void SpawnRing(Node3D player, string enemy, int count)
	{
		var spawner = GetParent().GetNodeOrNull<TestSpawner>("TestSpawner");
		PackedScene scene = null;
		if (spawner != null)
			foreach (PackedScene candidate in spawner.EnemyScenes)
				if (candidate != null && candidate.ResourcePath.GetFile().GetBaseName().ToLowerInvariant().StartsWith(enemy))
					scene = candidate;
		if (scene == null || player == null)
		{
			GD.PushWarning($"LaunchFlags: no enemy called '{enemy}'.");
			return;
		}
		Node parent = GetTree().CurrentScene;
		for (int i = 0; i < count; i++)
		{
			float angle = i * Mathf.Tau / count;
			Vector3 at = player.GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpawnRingRadius;
			var body = scene.Instantiate<Node3D>();
			body.Position = NavGrid.SnapToGround(at) + Vector3.Up * 0.1f;
			parent.AddChild(body);
		}
	}
}
