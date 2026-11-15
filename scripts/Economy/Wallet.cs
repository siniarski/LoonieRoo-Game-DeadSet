using Deadset.Core;
using Deadset.Enemies;
using Godot;

namespace Deadset.Economy;

/// <summary>
/// The player's cash. Add as a child "Wallet" of the player. Earns each enemy's bounty on kill.
/// (Solo for now: every kill pays this wallet. For co-op, pay whoever landed the kill.)
/// </summary>
public partial class Wallet : Node
{
	[Signal] public delegate void CashChangedEventHandler(int cash, int delta);

	[Export] public int StartingCash { get; set; } = 60;

	public int Cash { get; private set; }

	public override void _Ready()
	{
		Cash = StartingCash;
		GameEvents.Instance.EnemyKilled += OnEnemyKilled;
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance != null)
			GameEvents.Instance.EnemyKilled -= OnEnemyKilled;
	}

	public void Add(int amount)
	{
		if (amount == 0)
			return;
		Cash += amount;
		EmitSignal(SignalName.CashChanged, Cash, amount);
	}

	/// <summary>Spends cash if there's enough. Returns false (and spends nothing) otherwise.</summary>
	public bool TrySpend(int amount)
	{
		if (amount < 0 || amount > Cash)
			return false;
		Add(-amount);
		return true;
	}

	private void OnEnemyKilled(Node3D enemy)
	{
		if (enemy is Enemy { Stats: not null } e)
			Add(e.Stats.Bounty);
	}

	/// <summary>The wallet on a player node, if it has one.</summary>
	public static Wallet FindOn(Node player) => player?.GetNodeOrNull<Wallet>("Wallet");
}
