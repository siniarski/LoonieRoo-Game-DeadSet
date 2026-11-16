using System.Collections.Generic;
using System.Threading.Tasks;
using Deadset.Combat;
using Deadset.Core;
using Deadset.Economy;
using Deadset.Enemies;
using Deadset.Player;
using Deadset.Upgrades;
using Deadset.Waves;
using Deadset.Weapons;
using Godot;

namespace Deadset.Debug;

/// <summary>
/// Automated checks that run the real game, for when nobody is playing it. Started by the
/// --check launch flag (check.cmd runs them all). Each check prints "CHECK PASS" or
/// "CHECK FAIL" with a detail line; the game quits with the number of failures as its exit code.
///
///   --check            reach, weapons, trader, perf
///   --check=flags      the launch flags given alongside actually took effect (check.cmd passes
///                      --weapon=rivet --mod=rivet_pierce:2 --upgrade=quick_loader --cash=100)
///   --check=reach,perf any comma-separated subset
/// </summary>
public partial class SelfCheck : Node
{
	public string Which { get; set; } = "";

	/// <summary>Average process + physics time allowed with 200 enemies, in ms (headless, no rendering).</summary>
	[Export] public float PerfBudgetMs { get; set; } = 9f;
	/// <summary>Seconds of game time enemies get to reach the player from the far gates.</summary>
	[Export] public float ReachSeconds { get; set; } = 22f;

	private PlayerController _player;
	private PlayerWeaponInput _arsenal;
	private Trader _trader;
	private int _failures;

	public override void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		_arsenal = _player.GetNode<PlayerWeaponInput>("WeaponInput");
		_trader = GetTree().GetFirstNodeInGroup("trader") as Trader;
		HealthComponent.FindOn(_player)?.AddDamageModifier(_ => 0f);
		_player.SetProcessUnhandledInput(false);
		_arsenal.SetProcessUnhandledInput(false);
		CallDeferred(MethodName.Run);
	}

	private async void Run()
	{
		while (!NavGrid.Built)
			await Frames(1);
		await Frames(5);

		string which = string.IsNullOrEmpty(Which) ? "reach,flank,weapons,trader,perf" : Which;
		foreach (string check in which.Split(','))
		{
			switch (check.Trim())
			{
				case "flags": CheckFlags(); break;
				case "reach": await CheckReach(); break;
				case "weapons": await CheckWeapons(); break;
				case "trader": CheckTrader(); break;
				case "perf": await CheckPerf(); break;
				case "flank": await CheckFlank(); break;
				default: Fail(check, "unknown check"); break;
			}
			ClearEnemies();
			await Frames(3);
		}

		GD.Print($"CHECK DONE: {_failures} failed");
		Engine.TimeScale = 1f;
		GetTree().Quit(_failures);
	}

	// ---- Helpers ----------------------------------------------------------------------------

	private void Pass(string name, string detail) => GD.Print($"CHECK PASS {name}: {detail}");

	private void Fail(string name, string detail)
	{
		_failures++;
		GD.Print($"CHECK FAIL {name}: {detail}");
	}

	private void Expect(bool ok, string name, string detail)
	{
		if (ok)
			Pass(name, detail);
		else
			Fail(name, detail);
	}

	private async Task Frames(int count)
	{
		for (int i = 0; i < count; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	}

	private async Task Seconds(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds, processAlways: false), SceneTreeTimer.SignalName.Timeout);
	}

	private List<Node3D> Enemies()
	{
		var list = new List<Node3D>();
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			if (node is Node3D enemy && !enemy.IsQueuedForDeletion())
				list.Add(enemy);
		return list;
	}

	private void ClearEnemies()
	{
		foreach (Node3D enemy in Enemies())
			enemy.QueueFree();
	}

	private PackedScene EnemyScene(string name)
	{
		var spawner = GetParent()?.GetParent()?.GetNodeOrNull<TestSpawner>("TestSpawner");
		if (spawner == null)
			return null;
		foreach (PackedScene scene in spawner.EnemyScenes)
			if (scene != null && scene.ResourcePath.GetFile().GetBaseName().ToLowerInvariant() == name)
				return scene;
		return null;
	}

	private Node3D Spawn(PackedScene scene, Vector3 at, float chunky = 1f)
	{
		var enemy = scene.Instantiate<Node3D>();
		if (chunky > 1f && enemy is Enemy body)
			body.MakeChunky(chunky, 1.15f);
		enemy.Position = NavGrid.SnapToGround(at) + Vector3.Up * 0.1f;
		GetTree().CurrentScene.AddChild(enemy);
		return enemy;
	}

	private void Aim(Vector3 point)
	{
		var head = _player.GetNode<Node3D>("Head");
		Vector3 d = point - head.GetNode<Node3D>("Camera3D").GlobalPosition;
		_player.Rotation = new Vector3(0f, Mathf.Atan2(-d.X, -d.Z), 0f);
		head.Rotation = new Vector3(Mathf.Asin(Mathf.Clamp(d.Normalized().Y, -1f, 1f)), 0f, 0f);
	}

	private void Place(Vector3 at)
	{
		_player.GlobalPosition = at + Vector3.Up * 0.1f;
		_player.Velocity = Vector3.Zero;
	}

	// ---- Checks -----------------------------------------------------------------------------

	private void CheckFlags()
	{
		Weapon held = _arsenal.EquippedWeapon;
		Expect(held?.Stats.DisplayName == "Rivet Gun", "flags.weapon", $"holding {held?.Stats.DisplayName}");
		int pierce = 0;
		foreach (WeaponMod mod in held?.Stats.Mods ?? new())
			if (mod.Id == "rivet_pierce")
				pierce = held.LevelOf(mod);
		Expect(pierce == 2, "flags.mod", $"rivet_pierce level {pierce}");
		var upgrades = _player.GetNode<PlayerUpgrades>("Upgrades");
		int quick = 0;
		foreach (UpgradeDefinition upgrade in upgrades.Owned)
			if (upgrade.Id == "quick_loader")
				quick = upgrades.StacksOf(upgrade);
		Expect(quick == 1, "flags.upgrade", $"quick_loader stacks {quick}");
		int cash = Wallet.FindOn(_player).Cash;
		Expect(cash == Wallet.FindOn(_player).StartingCash + 100, "flags.cash", $"cash {cash}");
	}

	/// <summary>From every gate and vent, emus and a wombat reach each named player spot.</summary>
	private async Task CheckReach()
	{
		PackedScene emu = EnemyScene("emu");
		PackedScene wombat = EnemyScene("wombat");
		if (emu == null || wombat == null)
		{
			Fail("reach", "no emu or wombat scene on the TestSpawner");
			return;
		}
		var sources = new List<Node3D>();
		foreach (Node n in GetTree().GetNodesInGroup("enemy_spawn"))
			sources.Add((Node3D)n);
		foreach (Node n in GetTree().GetNodesInGroup("enemy_vent"))
			sources.Add((Node3D)n);

		Engine.TimeScale = 4f;
		foreach (Node spotNode in GetTree().GetNodesInGroup("player_spot"))
		{
			var spot = (Node3D)spotNode;
			Place(spot.GlobalPosition);
			await Frames(2);
			foreach (Node3D source in sources)
			{
				Spawn(emu, source.GlobalPosition);
				Spawn(source.IsInGroup("enemy_spawn") ? wombat : emu, source.GlobalPosition + new Vector3(0.8f, 0f, 0.6f));
			}
			await Seconds(ReachSeconds);
			int total = 0, near = 0;
			var stuck = new List<string>();
			foreach (Node3D enemy in Enemies())
			{
				total++;
				if (enemy.GlobalPosition.DistanceTo(_player.GlobalPosition) < 10f)
					near++;
				else if (stuck.Count < 4)
					stuck.Add($"({enemy.GlobalPosition.X:0},{enemy.GlobalPosition.Z:0})");
			}
			// A few may be queued behind the crowd at a doorway: allow 10%.
			Expect(near >= total * 0.9f, $"reach.{spot.Name}", $"{near}/{total} within 10 m" + (stuck.Count > 0 ? $"; others at {string.Join(' ', stuck)}" : ""));
			ClearEnemies();
			await Frames(3);
		}
		Engine.TimeScale = 1f;
	}

	/// <summary>
	/// The lane-funnel test. At each player spot, a pack of emus comes from the nearest gate
	/// (the worst case: one obvious way in). Counts the directions they arrive from, in eighths
	/// of a circle, and the share that reach the player from behind (the half facing away from
	/// the gate's arrivals). Run once with no flankers and once with the wave set's share.
	/// </summary>
	private async Task CheckFlank()
	{
		PackedScene emu = EnemyScene("emu");
		var gates = new List<Node3D>();
		foreach (Node n in GetTree().GetNodesInGroup("enemy_spawn"))
			gates.Add((Node3D)n);
		var director = GetTree().GetFirstNodeInGroup("wave_director") as WaveDirector;
		float share = director?.Waves?.FlankerShare ?? 0.25f;

		Engine.TimeScale = 4f;
		foreach (Node spotNode in GetTree().GetNodesInGroup("player_spot"))
		{
			var spot = (Node3D)spotNode;
			Node3D gate = null;
			foreach (Node3D g in gates)
				if (gate == null || g.GlobalPosition.DistanceTo(spot.GlobalPosition) < gate.GlobalPosition.DistanceTo(spot.GlobalPosition))
					gate = g;
			string line = "";
			float behindWith = 0f, behindWithout = 0f;
			foreach (float s in new[] { 0f, share })
			{
				Place(spot.GlobalPosition);
				await Frames(2);
				for (int i = 0; i < 24; i++)
				{
					var body = Spawn(emu, gate.GlobalPosition + new Vector3(i % 4 - 1.5f, 0f, i / 4 - 2.5f) * 0.9f) as Enemy;
					if (body != null)
						body.IsFlanker = i < Mathf.RoundToInt(24 * s);
				}
				var arrived = new Dictionary<Node3D, Vector3>();
				for (float t = 0f; t < 40f && arrived.Count < 24; t += 0.1f)
				{
					await Seconds(0.1f);
					foreach (Node3D enemy in Enemies())
					{
						Vector3 offset = enemy.GlobalPosition - _player.GlobalPosition;
						offset.Y = 0f;
						if (offset.Length() < 3f && !arrived.ContainsKey(enemy))
							arrived[enemy] = offset.Normalized();
					}
				}
				Vector3 main = Vector3.Zero;
				foreach (Vector3 d in arrived.Values)
					main += d;
				main = main.LengthSquared() > 0.0001f ? main.Normalized() : Vector3.Forward;
				var sectors = new HashSet<int>();
				int behind = 0;
				foreach (Vector3 d in arrived.Values)
				{
					sectors.Add(Mathf.PosMod(Mathf.RoundToInt(Mathf.Atan2(d.X, d.Z) / (Mathf.Tau / 8f)), 8));
					if (d.Dot(main) < 0f)
						behind++;
				}
				float behindShare = arrived.Count > 0 ? (float)behind / arrived.Count : 0f;
				if (s > 0f) behindWith = behindShare; else behindWithout = behindShare;
				line += $" | flankers {s:P0}: {arrived.Count}/24 arrived, {sectors.Count}/8 directions, {behindShare:P0} from behind";
				ClearEnemies();
				await Frames(3);
			}
			// A gate right next to the spot spills the pack straight onto the player: nothing to flank.
			bool tooClose = gate.GlobalPosition.DistanceTo(spot.GlobalPosition) < 10f;
			Expect(behindWith >= 0.15f || tooClose, $"flank.{spot.Name}", $"from {gate.Name}{line}{(tooClose ? " (gate too close to flank)" : "")}");
		}
		Engine.TimeScale = 1f;
	}

	/// <summary>Every weapon on sale, plus the nail gun, kills an emu in front of it.</summary>
	private async Task CheckWeapons()
	{
		PackedScene emu = EnemyScene("emu");
		Node startSpot = null;
		foreach (Node n in GetTree().GetNodesInGroup("player_spot"))
			if (n.Name == "Start")
				startSpot = n;
		Vector3 origin = (startSpot as Node3D)?.GlobalPosition ?? Vector3.Zero;

		foreach (Trader.WeaponOffer offer in _trader.Offers)
			if (!_arsenal.Owns(offer.Stats))
				_arsenal.Give(offer.Scene);

		for (int slot = 0; slot < _arsenal.Weapons.Count; slot++)
		{
			Place(origin);
			_arsenal.EquipSlot(slot);
			Weapon weapon = _arsenal.EquippedWeapon;
			float distance = weapon is MeleeConeWeapon ? 1.8f : 7f;
			Node3D target = Spawn(emu, origin + Vector3.Forward * distance);
			if (target is CollisionObject3D body)
				body.DisableMode = CollisionObject3D.DisableModeEnum.KeepActive;
			target.ProcessMode = ProcessModeEnum.Disabled;
			await Frames(3);
			var head = target.GetNodeOrNull<Node3D>("HeadWeakSpot");
			Aim(weapon is MeleeConeWeapon ? target.GlobalPosition + Vector3.Up : head.GlobalPosition);
			int ammoBefore = weapon.AmmoInMagazine;
			weapon.SetTriggerHeld(true);
			await Seconds(1.5f);
			weapon.SetTriggerHeld(false);
			bool dead = !IsInstanceValid(target) || target.IsQueuedForDeletion();
			Expect(dead, $"weapons.{weapon.Stats.DisplayName}", $"emu at {distance} m {(dead ? "killed" : "survived")}, ammo {ammoBefore} -> {weapon.AmmoInMagazine}");
			ClearEnemies();
			await Frames(2);
		}
		_arsenal.EquipSlot(0);

		// A late-wave (twice as tough) emu still dies to one nail in the head.
		{
			Place(origin);
			Weapon nail = _arsenal.EquippedWeapon;
			Node3D target = Freeze(Spawn(emu, origin + Vector3.Forward * 7f, 2f));
			await Frames(3);
			Aim(target.GetNode<Node3D>("HeadWeakSpot").GlobalPosition);
			int before = nail.AmmoInMagazine;
			nail.SetTriggerHeld(true);
			await Frames(1);
			nail.SetTriggerHeld(false);
			await Seconds(0.5f);
			bool dead = !IsInstanceValid(target) || target.IsQueuedForDeletion();
			Expect(dead && before - nail.AmmoInMagazine == 1, "weapons.late-headshot", $"tough emu {(dead ? "killed" : "survived")} by {before - nail.AmmoInMagazine} nail(s)");
			ClearEnemies();
			await Frames(2);
		}

		// Every gun dry: the hero's melee still kills, and a hammer kill gives nails back.
		if (_arsenal.QuickMelee is { } melee)
		{
			Place(origin);
			foreach (Weapon weapon in _arsenal.Weapons)
				weapon.DrainAmmo();
			Node3D target = Freeze(Spawn(emu, origin + Vector3.Forward * 1.6f));
			await Frames(3);
			Aim(target.GetNode<Node3D>("HeadWeakSpot").GlobalPosition);
			_arsenal.MeleeHeld = true;
			await Seconds(0.3f);
			_arsenal.MeleeHeld = false;
			await Frames(2);
			bool dead = !IsInstanceValid(target) || target.IsQueuedForDeletion();
			int nails = _arsenal.Weapons[0].ReserveAmmo;
			Expect(dead && nails == melee.RefundOnKill, $"weapons.{melee.Stats.DisplayName}", $"dry, one head whack: emu {(dead ? "killed" : "survived")}, nails back {nails}");
			ClearEnemies();
			foreach (Weapon weapon in _arsenal.Weapons)
				weapon.AddReserveAmmo(weapon.Stats.StartingReserve);
			await Frames(2);
		}
	}

	private static Node3D Freeze(Node3D target)
	{
		if (target is CollisionObject3D body)
			body.DisableMode = CollisionObject3D.DisableModeEnum.KeepActive;
		target.ProcessMode = ProcessModeEnum.Disabled;
		return target;
	}

	/// <summary>The trader opens on a break, sells ammo, a mod and armour, and closes on the wave.</summary>
	private void CheckTrader()
	{
		Wallet wallet = Wallet.FindOn(_player);
		wallet.Add(5000);
		GameEvents.Instance.EmitSignal(GameEvents.SignalName.BreakStarted, 2, 30f);
		Expect(_trader.IsOpen, "trader.opens", $"open {_trader.IsOpen}, {_trader.Choices.Count} upgrade choices");

		Weapon nail = _arsenal.Weapons[0];
		nail.AddReserveAmmo(-nail.ReserveAmmo);
		int cash = wallet.Cash;
		int bought = _trader.BuyAmmo(nail, 50);
		Expect(bought == 50 && wallet.Cash == cash - Trader.CostOfAmmo(nail, 50), "trader.ammo", $"bought {bought}, paid {cash - wallet.Cash}");

		WeaponMod mod = nail.Stats.Mods[0];
		int level = nail.LevelOf(mod);
		cash = wallet.Cash;
		bool modOk = _trader.BuyMod(nail, mod);
		Expect(modOk && nail.LevelOf(mod) == level + 1 && wallet.Cash < cash, "trader.mod", $"{mod.Id} level {level} -> {nail.LevelOf(mod)}");

		int armour = _trader.BuyArmour(25);
		Expect(armour == 25, "trader.armour", $"bought {armour}");
		bool picked = _trader.Choices.Count == 0 || _trader.PickUpgrade(0);
		Expect(picked, "trader.upgrade", "picked the first choice");

		GameEvents.Instance.EmitSignal(GameEvents.SignalName.WaveStarted, 2, 0);
		Expect(!_trader.IsOpen, "trader.closes", $"open {_trader.IsOpen}");
	}

	/// <summary>200 enemies chasing the player stay within the CPU budget.</summary>
	private async Task CheckPerf()
	{
		var spawner = GetParent()?.GetParent()?.GetNodeOrNull<TestSpawner>("TestSpawner");
		if (spawner == null)
		{
			Fail("perf", "no TestSpawner");
			return;
		}
		Place(Vector3.Zero);
		spawner.SpawnHorde(200);
		await Seconds(8f);
		double total = 0;
		double worst = 0;
		int samples = 0;
		for (int i = 0; i < 180; i++)
		{
			await Frames(1);
			double ms = (Performance.GetMonitor(Performance.Monitor.TimeProcess)
				+ Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess)) * 1000.0;
			total += ms;
			worst = Mathf.Max(worst, ms);
			samples++;
		}
		double average = total / samples;
		Expect(average <= PerfBudgetMs, "perf.200",
			$"{Enemies().Count} alive, {average:0.0} ms average (budget {PerfBudgetMs}), worst {worst:0.0} ms");
	}
}
