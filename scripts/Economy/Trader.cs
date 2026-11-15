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
	/// <summary>Cash per nail. Fractions round up per purchase.</summary>
	[Export] public float PricePerNail { get; set; } = 0.5f;
	/// <summary>Cash per point of armour.</summary>
	[Export] public float PricePerArmour { get; set; } = 1.5f;
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

	// ---- Ammo ----

	public int AmmoSpace => Weapon?.ReserveSpace ?? 0;
	public int CostOfNails(int nails) => Mathf.CeilToInt(nails * PricePerNail);
	public int MagazineSize => Weapon?.Stats.MagazineSize ?? 0;

	/// <summary>Buys up to this many nails, limited by reserve space and cash. Returns nails bought.</summary>
	public int BuyNails(int nails)
	{
		if (!IsOpen || Weapon == null || Wallet == null)
			return 0;
		nails = Mathf.Min(nails, AmmoSpace);
		while (nails > 0 && CostOfNails(nails) > Wallet.Cash)
			nails--;
		if (nails <= 0 || !Wallet.TrySpend(CostOfNails(nails)))
			return 0;
		Weapon.AddReserveAmmo(nails);
		EmitSignal(SignalName.StockChanged);
		return nails;
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
