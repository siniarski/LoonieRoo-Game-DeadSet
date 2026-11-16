using System.Collections.Generic;
using System.Threading.Tasks;
using Deadset.Combat;
using Deadset.Economy;
using Deadset.Enemies;
using Deadset.Weapons;
using Godot;

namespace Deadset.Debug;

/// <summary>The "variants" check: every enemy variant spawns, and each special mechanic works.</summary>
public partial class SelfCheck
{
	private Enemy SpawnVariant(string enemy, string id, Vector3 at)
	{
		var body = EnemyScene(enemy).Instantiate<Enemy>();
		body.ApplyVariant(LaunchFlags.FindVariant(enemy, id));
		body.Position = NavGrid.SnapToGround(at) + Vector3.Up * 0.1f;
		GetTree().CurrentScene.AddChild(body);
		return body;
	}

	private int SlotOf(Weapon weapon)
	{
		for (int i = 0; i < _arsenal.Weapons.Count; i++)
			if (_arsenal.Weapons[i] == weapon)
				return i;
		return 0;
	}

	private async Task Shoot(Weapon weapon, Vector3 at)
	{
		Aim(at);
		await Frames(1);
		weapon.SetTriggerHeld(true);
		await Frames(1);
		weapon.SetTriggerHeld(false);
		await Seconds(1f / Mathf.Max(weapon.Stats.FireRate, 0.5f) + 0.05f);
	}

	private async Task CheckVariants()
	{
		Node startSpot = null;
		foreach (Node n in GetTree().GetNodesInGroup("player_spot"))
			if (n.Name == "Start")
				startSpot = n;
		Vector3 origin = (startSpot as Node3D)?.GlobalPosition ?? Vector3.Zero;
		HealthComponent playerHealth = HealthComponent.FindOn(_player);
		Place(origin);
		playerHealth.ResetHealth();

		// 1. Every variant file loads, spawns and wears its gear.
		int ok = 0, total = 0;
		var bad = new List<string>();
		foreach (string file in DirAccess.GetFilesAt("res://resources/enemies/variants"))
		{
			if (!file.EndsWith(".tres"))
				continue;
			total++;
			string name = file.GetBaseName();
			string enemy = name[..name.IndexOf('_')], id = name[(name.IndexOf('_') + 1)..];
			var body = (Enemy)Freeze(SpawnVariant(enemy, id, origin + new Vector3(0f, 0f, -14f)));
			await Frames(2);
			int gear = body.FindChildren("Gear*", "MeshInstance3D", true, false).Count;
			int plates = 0;
			foreach (GearPiece piece in body.Variant.Gear)
				if (piece.Armour > 0f)
					plates++;
			if (body.Variant.Id == id && gear == body.Variant.Gear.Count && body.Plates.Count == plates)
				ok++;
			else
				bad.Add($"{name} (gear {gear}/{body.Variant.Gear.Count}, plates {body.Plates.Count}/{plates})");
			ClearEnemies();
			await Frames(2);
		}
		Expect(ok == total && total > 0, "variants.spawn", $"{ok}/{total} spawned with their gear" + (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));

		foreach (Trader.WeaponOffer offer in _trader.Offers)
			if (!_arsenal.Owns(offer.Stats))
				_arsenal.Give(offer.Scene);
		Weapon nail = null, rivet = null;
		foreach (Weapon w in _arsenal.Weapons)
		{
			if (w.Stats.DisplayName == "Nail Gun") nail = w;
			if (w.Stats.DisplayName == "Rivet Gun") rivet = w;
		}

		// 2. Armour: a nail at an armoured wombat's face pings off; a rivet cracks the plate and goes through.
		{
			Place(origin);
			var wombat = (Enemy)Freeze(SpawnVariant("wombat", "armoured", origin + new Vector3(0f, 0f, -7f)));
			// Turn it to face the player (models face -Z; it spawned facing away).
			var visual = wombat.GetNode<Node3D>("Visual");
			visual.Rotation = new Vector3(0f, Mathf.Pi, 0f);
			await Frames(3);
			Vector3 face = wombat.GetNode<Node3D>("Visual/HeadPivot/Head").GlobalPosition;
			HealthComponent health = HealthComponent.FindOn(wombat);
			_arsenal.EquipSlot(SlotOf(nail));
			await Seconds(0.4f);
			int blocked = ArmourPlate.ShotsBlocked;
			float before = health.Current;
			await Shoot(nail, face);
			await Seconds(0.2f);
			bool pinged = ArmourPlate.ShotsBlocked > blocked && Mathf.IsEqualApprox(health.Current, before);
			_arsenal.EquipSlot(SlotOf(rivet));
			await Seconds(0.6f);
			int broken = ArmourPlate.PlatesBroken;
			await Shoot(rivet, face);
			await Seconds(0.3f);
			bool cracked = ArmourPlate.PlatesBroken > broken;
			bool through = !IsInstanceValid(wombat) || health.Current < before;
			Expect(pinged && cracked && through, "variants.armour", $"nail pinged off {pinged}, rivet cracked the plate {cracked}, went through {through}");
			ClearEnemies();
			_arsenal.EquipSlot(0);
			await Frames(2);
		}

		// 3. Ranged: a mortar wombat lobs at a player standing still and hits.
		{
			Place(origin);
			playerHealth.ResetHealth();
			int hits = Lobbed.PlayerHits, thrown = Lobbed.Thrown;
			SpawnVariant("wombat", "mortar", origin + new Vector3(0f, 0f, -16f));
			for (float t = 0f; t < 10f && Lobbed.PlayerHits == hits; t += 0.25f)
			{
				Place(origin);
				await Seconds(0.25f);
			}
			Expect(Lobbed.PlayerHits > hits, "variants.mortar", $"thrown {Lobbed.Thrown - thrown}, hit {Lobbed.PlayerHits - hits}");
			ClearEnemies();
			await Frames(2);
		}

		// 4. Joey-pouch: dies and leaves a joey.
		{
			int spawns = Enemy.DeathSpawns;
			Enemy roo = SpawnVariant("kangaroo", "pouch", origin + new Vector3(0f, 0f, -12f));
			await Frames(3);
			HealthComponent.FindOn(roo).ApplyDamage(10000f);
			await Frames(3);
			bool joey = false;
			foreach (Node3D e in Enemies())
				if (e is Enemy { Variant.Id: "joey" })
					joey = true;
			Expect(Enemy.DeathSpawns > spawns && joey, "variants.pouch", $"joey out {joey}");
			ClearEnemies();
			await Frames(2);
		}

		// 5. Brooder: comes with chicks; kill one and she enrages.
		{
			Enemy mum = SpawnVariant("cassowary", "brooder", origin + new Vector3(0f, 0f, -14f));
			await Frames(4);
			var chicks = new List<Enemy>();
			foreach (Node3D e in Enemies())
				if (e is Enemy { Variant.Id: "chick" } chick && chick.EscortOf == mum)
					chicks.Add(chick);
			bool calm = !mum.IsEnraged;
			if (chicks.Count > 0)
				HealthComponent.FindOn(chicks[0]).ApplyDamage(10000f);
			await Frames(3);
			Expect(chicks.Count == 3 && calm && mum.IsEnraged, "variants.brooder", $"chicks {chicks.Count}, calm first {calm}, enraged after {mum.IsEnraged}");
			ClearEnemies();
			await Frames(2);
		}

		// 6. Short fuse: enrages below half health.
		{
			Enemy cass = SpawnVariant("cassowary", "enraged", origin + new Vector3(0f, 0f, -14f));
			await Frames(3);
			HealthComponent h = HealthComponent.FindOn(cass);
			h.ApplyDamage(h.MaxHealth * 0.6f);
			await Frames(3);
			Expect(cass.IsEnraged, "variants.enraged", $"enraged at {h.Current / h.MaxHealth:P0} health: {cass.IsEnraged}");
			ClearEnemies();
			await Frames(2);
		}

		// 7. Alpha: the pack trails it; kill it and they scatter.
		{
			Place(origin);
			Enemy alpha = SpawnVariant("dingo", "alpha", origin + new Vector3(0f, 0f, -24f));
			var pack = new List<MeleeChaser>();
			for (int i = 0; i < 3; i++)
			{
				var d = (MeleeChaser)EnemyScene("dingo").Instantiate<Enemy>();
				d.Leader = alpha;
				d.Position = NavGrid.SnapToGround(origin + new Vector3(i * 2f - 2f, 0f, -27f)) + Vector3.Up * 0.1f;
				GetTree().CurrentScene.AddChild(d);
				pack.Add(d);
			}
			await Seconds(1.5f);
			float spread = 0f;
			foreach (MeleeChaser d in pack)
				spread = Mathf.Max(spread, d.GlobalPosition.DistanceTo(alpha.GlobalPosition));
			HealthComponent.FindOn(alpha).ApplyDamage(10000f);
			await Seconds(0.3f);
			int scattered = 0;
			foreach (MeleeChaser d in pack)
				if (IsInstanceValid(d) && d.Scattered)
					scattered++;
			Expect(spread < 6f && scattered == 3, "variants.alpha", $"pack within {spread:0.0} m of the alpha, scattered after it died {scattered}/3");
			ClearEnemies();
			await Frames(2);
		}

		// 8. Howler: stops at range and hurries the others.
		{
			Place(origin);
			int howls = Enemy.Howls;
			SpawnVariant("dingo", "howler", origin + new Vector3(0f, 0f, -16f));
			Freeze(Spawn(EnemyScene("emu"), origin + new Vector3(4f, 0f, -18f)));
			for (float t = 0f; t < 8f && Enemy.Howls == howls; t += 0.25f)
			{
				Place(origin);
				await Seconds(0.25f);
			}
			Expect(Enemy.Howls > howls, "variants.howler", $"howls {Enemy.Howls - howls}");
			ClearEnemies();
			await Frames(2);
		}

		// 9. Burrower: goes under at range and pops up close.
		{
			Place(origin);
			int ups = Wombat.Surfacings;
			Enemy mole = SpawnVariant("wombat", "burrower", origin + new Vector3(0f, 0f, -22f));
			float nearest = float.MaxValue;
			for (float t = 0f; t < 10f && Wombat.Surfacings == ups; t += 0.2f)
			{
				Place(origin);
				await Seconds(0.2f);
			}
			if (IsInstanceValid(mole))
				nearest = mole.GlobalPosition.DistanceTo(origin);
			Expect(Wombat.Surfacings > ups && nearest < 10f, "variants.burrower", $"surfaced {Wombat.Surfacings - ups}, at {nearest:0.0} m");
			ClearEnemies();
			await Frames(2);
		}

		// 10. Glowneck: a head kill splashes the flock around it.
		{
			Place(origin);
			var glow = (Enemy)Freeze(SpawnVariant("emu", "glowneck", origin + new Vector3(0f, 0f, -7f)));
			var flock = new List<HealthComponent>();
			for (int i = 0; i < 3; i++)
				flock.Add(HealthComponent.FindOn(Freeze(Spawn(EnemyScene("emu"), origin + new Vector3(i * 1.2f - 1.2f, 0f, -9f)))));
			await Frames(3);
			_arsenal.EquipSlot(SlotOf(nail));
			await Seconds(0.4f);
			await Shoot(nail, glow.GetNode<Node3D>("HeadWeakSpot").GlobalPosition);
			await Seconds(0.3f);
			int hurt = 0;
			foreach (HealthComponent h in flock)
				if (!IsInstanceValid(h) || h.IsDead || h.Current < h.MaxHealth)
					hurt++;
			Expect(!IsInstanceValid(glow) && hurt == 3, "variants.glowneck", $"glowneck popped {!IsInstanceValid(glow)}, flock hurt {hurt}/3");
			ClearEnemies();
			await Frames(2);
		}

		// 11. Squadron: birds dive one after another.
		{
			Place(origin);
			int dives = Swooper.Dives;
			for (int i = 0; i < 3; i++)
				SpawnVariant("magpie", "squadron", origin + new Vector3(Mathf.Cos(i * 2.1f) * 9f, 0f, Mathf.Sin(i * 2.1f) * 9f));
			for (float t = 0f; t < 9f; t += 0.25f)
			{
				Place(origin);
				playerHealth.ResetHealth();
				await Seconds(0.25f);
			}
			Expect(Swooper.Dives - dives >= 4, "variants.squadron", $"dives in 9 s: {Swooper.Dives - dives}");
			ClearEnemies();
			await Frames(2);
		}
		Place(origin);
		playerHealth.ResetHealth();
	}
}
