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

	[Export] public int StartingCash { get; set; } = 200;

	[ExportGroup("Late-wave bounty taper")]
	/// <summary>Kills pay full bounty up to and including this wave.</summary>
	[Export] public int TaperStartWave { get; set; } = 5;
	/// <summary>From this wave on, kills pay only BountyFloor of their bounty.</summary>
	[Export] public int TaperEndWave { get; set; } = 9;
	[Export(PropertyHint.Range, "0,1")] public float BountyFloor { get; set; } = 0.6f;

	/// <summary>Share of bounty kills pay in the current wave.</summary>
	public float BountyMultiplier { get; private set; } = 1f;

	public int Cash { get; private set; }

	public override void _Ready()
	{
		Cash = StartingCash;
		GameEvents.Instance.EnemyKilled += OnEnemyKilled;
		GameEvents.Instance.WaveStarted += OnWaveStarted;
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance == null)
			return;
		GameEvents.Instance.EnemyKilled -= OnEnemyKilled;
		GameEvents.Instance.WaveStarted -= OnWaveStarted;
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
			Add(Mathf.Max(1, Mathf.RoundToInt(e.Stats.Bounty * BountyMultiplier)));
	}

	private void OnWaveStarted(int wave, int count)
	{
		float t = Mathf.Clamp((wave - TaperStartWave) / (float)Mathf.Max(TaperEndWave - TaperStartWave, 1), 0f, 1f);
		BountyMultiplier = Mathf.Lerp(1f, BountyFloor, t);
	}

	/// <summary>The wallet on a player node, if it has one.</summary>
	public static Wallet FindOn(Node player) => player?.GetNodeOrNull<Wallet>("Wallet");
}
