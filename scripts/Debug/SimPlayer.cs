using System.Collections.Generic;
using System.Text;
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
/// A simulated player for tuning: plays full runs and logs the economy and difficulty per wave.
/// Not a good player on purpose: it aims with human-like error and reaction time, kites away
/// from the crowd, reloads, and shops by a fixed plan. Start it with the --sim launch flag:
///
///   play.cmd --sim=nail          nail gun only (ammo, armour, nail mods)
///   play.cmd --sim=rivet         also buys the rivet gun as soon as it can
///   play.cmd --sim=cutter        also buys the brushcutter as soon as it can
///   --simspeed=3 runs faster; add --headless before "--" on the Godot command line for no window.
///
/// Lines starting "SIM" go to the console; the run quits after death or --simwaves (default 10).
/// </summary>
public partial class SimPlayer : Node
{
	public string Loadout { get; set; } = "nail";
	public int MaxWaves { get; set; } = 10;

	[Export] public float AimError { get; set; } = 0.22f;
	[Export] public float ReactionTime { get; set; } = 0.35f;
	/// <summary>Enemies inside this radius push the bot away.</summary>
	[Export] public float ThreatRadius { get; set; } = 9f;

	private PlayerController _player;
	private PlayerWeaponInput _arsenal;
	private HealthComponent _health;
	private Wallet _wallet;
	private Trader _trader;
	private RunStats _stats;
	private Camera3D _camera;
	private Node3D _head;

	private Node3D _target;
	private Vector3 _aimOffset;
	private float _retargetTimer;
	private float _shopTimer = -1f;
	private int _shots;
	private int _cashAtWaveStart;
	private float _hpAtWaveStart;
	private int _wave;
	private readonly StringBuilder _bought = new();
	private readonly HashSet<Weapon> _bound = new();

	public override void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		_arsenal = _player.GetNode<PlayerWeaponInput>("WeaponInput");
		_health = HealthComponent.FindOn(_player);
		_wallet = Wallet.FindOn(_player);
		_trader = GetTree().GetFirstNodeInGroup("trader") as Trader;
		_stats = GetTree().GetFirstNodeInGroup("run_stats") as RunStats;
		_head = _player.GetNode<Node3D>("Head");
		_camera = _head.GetNode<Camera3D>("Camera3D");

		// The bot drives the body; the real mouse and keyboard must not.
		_player.SetProcessUnhandledInput(false);
		_arsenal.SetProcessUnhandledInput(false);
		Input.MouseMode = Input.MouseModeEnum.Visible;

		GameEvents.Instance.WaveStarted += OnWaveStarted;
		GameEvents.Instance.WaveCleared += OnWaveCleared;
		GameEvents.Instance.BreakStarted += (_, _) => _shopTimer = 0.6f;
		_health.Died += OnDied;
		_arsenal.WeaponAdded += BindShots;
		foreach (Weapon weapon in _arsenal.Weapons)
			BindShots(weapon);
		GD.Print($"SIM start loadout={Loadout} aimError={AimError} reaction={ReactionTime}");
	}

	public override void _ExitTree()
	{
		foreach (StringName action in new StringName[] { "move_forward", "move_back", "move_left", "move_right", "sprint" })
			Input.ActionRelease(action);
		if (GameEvents.Instance == null)
			return;
		GameEvents.Instance.WaveStarted -= OnWaveStarted;
		GameEvents.Instance.WaveCleared -= OnWaveCleared;
	}

	private void BindShots(Weapon weapon)
	{
		if (_bound.Add(weapon))
			weapon.Fired += (_, _) => _shots++;
	}

	// ---- Reporting ----------------------------------------------------------------------

	private void OnWaveStarted(int wave, int count)
	{
		_wave = wave;
		_shots = 0;
		_cashAtWaveStart = _wallet.Cash;
		_hpAtWaveStart = _health.Current + _health.Armour;
	}

	private void OnWaveCleared(int wave)
	{
		RunStats.WaveSummary w = _stats.LastWave;
		GD.Print($"SIM wave {wave} cleared {w.Seconds:0}s | kills {w.Kills} | headshots {w.HeadshotPercent:0}% | "
			+ $"took {w.DamageTaken:0} dmg, hp {_health.Current:0} armour {_health.Armour:0} | shots {_shots} | "
			+ $"earned ${w.CashEarned}, cash ${_wallet.Cash} | {AmmoLine()}");
		if (wave >= MaxWaves)
			Finish($"reached wave {MaxWaves}");
	}

	private void OnDied()
	{
		Finish($"died on wave {_wave} ({(_stats?.Kills ?? 0)} kills)");
	}

	private string AmmoLine()
	{
		var line = new StringBuilder();
		foreach (Weapon weapon in _arsenal.Weapons)
			if (weapon.Stats.UsesAmmo)
				line.Append($"{weapon.Stats.AmmoName} {weapon.AmmoInMagazine}+{weapon.ReserveAmmo} ");
		return line.ToString().TrimEnd();
	}

	private void Finish(string how)
	{
		double minutes = (_stats?.Seconds ?? 0f) / 60.0;
		GD.Print($"SIM end: {how}, {minutes:0.0} game minutes, loadout {Loadout}");
		GetTree().Quit();
	}

	// ---- Shopping -----------------------------------------------------------------------

	private void Shop()
	{
		_bought.Clear();
		int before = _wallet.Cash;

		// 1. Keep enough ammo for the guns we rely on.
		foreach (Weapon weapon in _arsenal.Weapons)
			if (weapon.Stats.UsesAmmo)
				Note($"{_trader.BuyAmmo(weapon, Mathf.Min(Trader.AmmoSpace(weapon), weapon.MagazineSize * 6))} {weapon.Stats.AmmoName}");

		// 2. The loadout's weapon, as soon as it's affordable with $40 to spare.
		foreach (Trader.WeaponOffer offer in _trader.Offers)
		{
			string name = offer.Stats.DisplayName.ToLowerInvariant();
			bool wanted = (Loadout == "rivet" && name.Contains("rivet")) || (Loadout == "cutter" && name.Contains("brush"));
			if (wanted && !_trader.Owns(offer.Stats) && _wallet.Cash >= offer.Stats.Price + 40 && _trader.BuyWeapon(offer))
				Note($"BOUGHT {offer.Stats.DisplayName}");
		}
		foreach (Weapon weapon in _arsenal.Weapons)
			if (weapon.Stats.UsesAmmo)
				_trader.BuyAmmo(weapon, Trader.AmmoSpace(weapon) / 2);

		// 3. Armour up to half.
		if (_health.Armour < 50f)
			Note($"{_trader.BuyArmour((int)(50f - _health.Armour))} armour");

		// 4. Mods for the weapon the loadout is about, cheapest first, keeping $80 for ammo.
		Weapon focus = _arsenal.Weapons[_arsenal.Weapons.Count - 1];
		bool boughtMod = true;
		while (boughtMod)
		{
			boughtMod = false;
			WeaponMod cheapest = null;
			int cheapestPrice = int.MaxValue;
			foreach (WeaponMod mod in focus.Stats.Mods)
			{
				int price = Trader.NextModPrice(focus, mod);
				if (price > 0 && price < cheapestPrice)
				{
					cheapest = mod;
					cheapestPrice = price;
				}
			}
			if (cheapest != null && _wallet.Cash - cheapestPrice >= 80 && _trader.BuyMod(focus, cheapest))
			{
				Note($"{cheapest.Id} L{focus.LevelOf(cheapest)}");
				boughtMod = true;
			}
		}

		// 5. Top up everything with what's left.
		foreach (Weapon weapon in _arsenal.Weapons)
			if (weapon.Stats.UsesAmmo)
				_trader.BuyAmmo(weapon, Trader.AmmoSpace(weapon));
		_trader.BuyArmour(_trader.ArmourSpace);

		// 6. The first upgrade on offer (a player can't know which is best either).
		if (_trader.CanPick)
		{
			Note($"upgrade {_trader.Choices[0].DisplayName}");
			_trader.PickUpgrade(0);
		}
		GD.Print($"SIM  trader before wave {_wave + 1}: spent ${before - _wallet.Cash}, left ${_wallet.Cash} | {_bought}| {AmmoLine()}");

		// Ready up.
		(GetTree().GetFirstNodeInGroup("wave_director") as WaveDirector)?.SkipCountdown();
	}

	private void Note(string text)
	{
		if (!text.StartsWith("0 "))
			_bought.Append(text).Append(", ");
	}

	// ---- Fighting -----------------------------------------------------------------------

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_health.IsDead)
			return;

		if (_shopTimer > 0f)
		{
			_shopTimer -= dt;
			if (_shopTimer <= 0f && _trader.IsOpen)
				Shop();
		}

		Vector3 me = _player.GlobalPosition;
		Node3D nearest = null;
		float nearestDistance = float.MaxValue;
		int close = 0;
		Vector3 push = Vector3.Zero;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemy || enemy.IsQueuedForDeletion())
				continue;
			Vector3 offset = me - enemy.GlobalPosition;
			offset.Y = 0f;
			float distance = offset.Length();
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = enemy;
			}
			if (distance < 3f)
				close++;
			if (distance < ThreatRadius && distance > 0.01f)
				push += offset / (distance * distance);
		}

		Move(me, push);
		Fight(nearest, nearestDistance, close, dt);
	}

	/// <summary>Backs away from the crowd, steering round anything that isn't open ground.</summary>
	private void Move(Vector3 me, Vector3 push)
	{
		Vector3 wish = push.LengthSquared() > 0.0001f ? push.Normalized() : Vector3.Zero;
		// Drift back towards the middle when near the walls, so it doesn't get pinned.
		Vector3 toCentre = new Vector3(-me.X, 0f, -me.Z);
		if (toCentre.Length() > 20f)
			wish = (wish + toCentre.Normalized() * 0.6f).Normalized();

		if (wish != Vector3.Zero && !NavGrid.IsOpenGround(me + wish * 2f))
		{
			// Try the nearest open direction instead.
			Vector3 best = Vector3.Zero;
			float bestDot = -2f;
			for (int i = 0; i < 16; i++)
			{
				Vector3 dir = Vector3.Forward.Rotated(Vector3.Up, i * Mathf.Tau / 16f);
				if (!NavGrid.IsOpenGround(me + dir * 2f))
					continue;
				float dot = dir.Dot(wish);
				if (dot > bestDot)
				{
					bestDot = dot;
					best = dir;
				}
			}
			wish = best;
		}

		// World direction -> the player's local move input.
		Basis basis = _player.GlobalBasis;
		float right = wish.Dot(basis.X);
		float back = wish.Dot(basis.Z);
		SetAxis("move_right", "move_left", right);
		SetAxis("move_back", "move_forward", back);
		Input.ActionRelease("sprint");
	}

	private static void SetAxis(string positive, string negative, float value)
	{
		if (value > 0.15f)
		{
			Input.ActionPress(positive, Mathf.Min(value, 1f));
			Input.ActionRelease(negative);
		}
		else if (value < -0.15f)
		{
			Input.ActionPress(negative, Mathf.Min(-value, 1f));
			Input.ActionRelease(positive);
		}
		else
		{
			Input.ActionRelease(positive);
			Input.ActionRelease(negative);
		}
	}

	private void Fight(Node3D nearest, float distance, int close, float dt)
	{
		// Weapon choice: brushcutter when crowded, rivet gun when it has rivets, else nails.
		Weapon want = null;
		foreach (Weapon weapon in _arsenal.Weapons)
		{
			bool melee = weapon is MeleeConeWeapon;
			if (melee && close >= 2 && weapon.AmmoInMagazine + weapon.ReserveAmmo > 0)
				want = weapon;
			else if (!melee && want == null && weapon.Stats.DisplayName.Contains("Rivet") && weapon.AmmoInMagazine + weapon.ReserveAmmo > 0)
				want = weapon;
		}
		want ??= _arsenal.Weapons[0];
		if (_arsenal.EquippedWeapon != want)
			_arsenal.Equip(want);
		Weapon gun = _arsenal.EquippedWeapon;

		_retargetTimer -= dt;
		if (_retargetTimer <= 0f || !IsInstanceValid(_target) || _target.IsQueuedForDeletion())
		{
			_retargetTimer = ReactionTime;
			_target = nearest;
			_aimOffset = new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)) * AimError;
		}
		if (!IsInstanceValid(_target) || distance > 40f)
		{
			gun.SetTriggerHeld(false);
			return;
		}

		Vector3 aim = (_target.GetNodeOrNull<Node3D>("HeadWeakSpot")?.GlobalPosition ?? _target.GlobalPosition + Vector3.Up) + _aimOffset;
		Vector3 d = aim - _camera.GlobalPosition;
		_player.Rotation = new Vector3(0f, Mathf.Atan2(-d.X, -d.Z), 0f);
		_head.Rotation = new Vector3(Mathf.Asin(Mathf.Clamp(d.Normalized().Y, -1f, 1f)), 0f, 0f);

		bool inReach = gun is not MeleeConeWeapon || distance < gun.Stats.MaxRange * gun.ReachMultiplier + 0.5f;
		gun.SetTriggerHeld(inReach);
		if (gun.Stats.UsesAmmo && gun.AmmoInMagazine == 0)
			gun.RequestReload();
	}
}
