using Deadset.Combat;
using Deadset.Core;
using Deadset.Player;
using Deadset.Upgrades;
using Deadset.Weapons;
using Godot;
using Godot.Collections;

namespace Deadset.Economy;

/// <summary>
/// The trader break: open between waves. Sells ammo and armour for cash and offers
/// a 1-of-3 upgrade pick. Holds the shop rules and state; the TraderMenu only displays it.
/// </summary>
public partial class Trader : Node
{
	[Signal] public delegate void OpenedEventHandler();
	[Signal] public delegate void ClosedEventHandler();
	/// <summary>Anything shown in the shop changed (a purchase, a pick).</summary>
	[Signal] public delegate void StockChangedEventHandler();

	[ExportGroup("Prices")]
	/// <summary>Weapons the trader sells. Ammo prices and mods come from each weapon's stats.</summary>
	[Export] public Array<PackedScene> WeaponsForSale { get; set; } = new();
	/// <summary>Cash per point of armour.</summary>
	[Export] public float PricePerArmour { get; set; } = 2f;
	/// <summary>Armour points per small armour purchase.</summary>
	[Export] public int ArmourStep { get; set; } = 25;

	[ExportGroup("Upgrades")]
	[Export] public int UpgradeChoiceCount { get; set; } = 3;

	public bool IsOpen { get; private set; }
	public Array<UpgradeDefinition> Choices { get; private set; } = new();
	/// <summary>Index of the upgrade taken this break, or -1.</summary>
	public int PickedIndex { get; private set; } = -1;

	public Wallet Wallet { get; private set; }
	public HealthComponent Health { get; private set; }
	public PlayerUpgrades Upgrades { get; private set; }
	public Weapon Weapon => _weaponInput?.EquippedWeapon;
	public PlayerWeaponInput Arsenal => _weaponInput;

	/// <summary>A weapon on sale and the stats read from its scene.</summary>
	public readonly struct WeaponOffer
	{
		public PackedScene Scene { get; init; }
		public WeaponStats Stats { get; init; }
	}

	public System.Collections.Generic.List<WeaponOffer> Offers { get; } = new();

	private PlayerWeaponInput _weaponInput;

	public override void _Ready()
	{
		AddToGroup("trader");
		Node player = GetTree().GetFirstNodeInGroup("player");
		Wallet = Wallet.FindOn(player);
		Health = HealthComponent.FindOn(player);
		Upgrades = player?.GetNodeOrNull<PlayerUpgrades>("Upgrades");
		_weaponInput = player?.GetNodeOrNull<PlayerWeaponInput>("WeaponInput");

		if (Wallet != null)
			Wallet.CashChanged += (_, _) => EmitSignal(SignalName.StockChanged);

		foreach (PackedScene scene in WeaponsForSale)
		{
			if (scene == null)
				continue;
			// Read the stats once; the weapon itself is only created when bought.
			Node probe = scene.Instantiate();
			if (probe is Weapon weapon && weapon.Stats != null)
				Offers.Add(new WeaponOffer { Scene = scene, Stats = weapon.Stats });
			probe.Free();
		}

		GameEvents.Instance.BreakStarted += OnBreakStarted;
		GameEvents.Instance.WaveStarted += OnWaveStarted;
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance == null)
			return;
		GameEvents.Instance.BreakStarted -= OnBreakStarted;
		GameEvents.Instance.WaveStarted -= OnWaveStarted;
	}

	private void OnBreakStarted(int nextWave, float seconds)
	{
		IsOpen = true;
		PickedIndex = -1;
		Choices = Upgrades?.RollChoices(UpgradeChoiceCount) ?? new Array<UpgradeDefinition>();
		EmitSignal(SignalName.Opened);
	}

	private void OnWaveStarted(int wave, int count)
	{
		if (!IsOpen)
			return;
		IsOpen = false;
		EmitSignal(SignalName.Closed);
	}

	// ---- Weapons ----

	public bool Owns(WeaponStats stats) => _weaponInput?.Owns(stats) ?? false;

	public int OfferCount => Offers.Count;

	/// <summary>Buys the offer at this index (for tests and scripts).</summary>
	public bool BuyWeaponAt(int index) => index >= 0 && index < Offers.Count && BuyWeapon(Offers[index]);

	/// <summary>Buys a weapon and puts it in the player's hands. Returns true on success.</summary>
	public bool BuyWeapon(WeaponOffer offer)
	{
		if (!IsOpen || Wallet == null || _weaponInput == null || Owns(offer.Stats))
			return false;
		if (!Wallet.TrySpend(offer.Stats.Price))
			return false;
		_weaponInput.Give(offer.Scene);
		EmitSignal(SignalName.StockChanged);
		return true;
	}

	// ---- Ammo ----

	public static int AmmoSpace(Weapon weapon) => weapon?.ReserveSpace ?? 0;
	public static int CostOfAmmo(Weapon weapon, int rounds) =>
		weapon == null ? 0 : Mathf.CeilToInt(rounds * weapon.Stats.PricePerRound);

	/// <summary>Buys up to this many rounds, limited by reserve space and cash. Returns rounds bought.</summary>
	public int BuyAmmo(Weapon weapon, int rounds)
	{
		if (!IsOpen || weapon == null || Wallet == null)
			return 0;
		rounds = Mathf.Min(rounds, AmmoSpace(weapon));
		while (rounds > 0 && CostOfAmmo(weapon, rounds) > Wallet.Cash)
			rounds--;
		if (rounds <= 0 || !Wallet.TrySpend(CostOfAmmo(weapon, rounds)))
			return 0;
		weapon.AddReserveAmmo(rounds);
		EmitSignal(SignalName.StockChanged);
		return rounds;
	}

	// ---- Weapon mods ----

	/// <summary>Price of the next level of a mod on this weapon, or -1 when it is maxed.</summary>
	public static int NextModPrice(Weapon weapon, WeaponMod mod)
	{
		int level = weapon.LevelOf(mod);
		return level >= mod.MaxLevel ? -1 : mod.PriceForLevel(level + 1);
	}

	public bool BuyMod(Weapon weapon, WeaponMod mod)
	{
		if (!IsOpen || weapon == null || mod == null || Wallet == null)
			return false;
		int price = NextModPrice(weapon, mod);
		if (price < 0 || !Wallet.TrySpend(price))
			return false;
		weapon.AddModLevel(mod);
		EmitSignal(SignalName.StockChanged);
		return true;
	}

	// ---- Armour ----

	public int ArmourSpace => Health == null ? 0 : Mathf.FloorToInt(Health.MaxArmour - Health.Armour);
	public int CostOfArmour(int points) => Mathf.CeilToInt(points * PricePerArmour);

	/// <summary>Buys up to this many armour points, limited by space and cash. Returns points bought.</summary>
	public int BuyArmour(int points)
	{
		if (!IsOpen || Health == null || Wallet == null)
			return 0;
		points = Mathf.Min(points, ArmourSpace);
		while (points > 0 && CostOfArmour(points) > Wallet.Cash)
			points--;
		if (points <= 0 || !Wallet.TrySpend(CostOfArmour(points)))
			return 0;
		Health.AddArmour(points);
		EmitSignal(SignalName.StockChanged);
		return points;
	}

	// ---- Upgrades ----

	public bool CanPick => IsOpen && PickedIndex < 0 && Choices.Count > 0;

	public bool PickUpgrade(int index)
	{
		if (!CanPick || index < 0 || index >= Choices.Count || Upgrades == null)
			return false;
		PickedIndex = index;
		Upgrades.Grant(Choices[index]);
		EmitSignal(SignalName.StockChanged);
		return true;
	}
}
