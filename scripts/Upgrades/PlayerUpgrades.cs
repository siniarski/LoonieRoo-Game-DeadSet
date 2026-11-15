using System;
using System.Collections.Generic;
using Deadset.Combat;
using Deadset.Core;
using Deadset.Player;
using Deadset.Weapons;
using Godot;

namespace Deadset.Upgrades;

/// <summary>
/// Holds the upgrades one player owns and applies them. Lives as the "Upgrades" child of
/// the player. The picker UI asks it for choices and tells it what was picked; the player
/// class itself never hears about upgrades.
/// </summary>
public partial class PlayerUpgrades : Node
{
	[Signal] public delegate void UpgradeGrantedEventHandler(UpgradeDefinition upgrade, int stacks);

	/// <summary>The upgrades that can be offered.</summary>
	[Export] public UpgradePool Pool { get; set; }
	[Export] public NodePath HealthPath { get; set; } = "../HealthComponent";
	[Export] public NodePath WeaponInputPath { get; set; } = "../WeaponInput";

	/// <summary>Owned upgrades in the order they were first picked (each listed once).</summary>
	public IReadOnlyList<UpgradeDefinition> Owned => _owned;

	// Game events relayed for upgrade hooks. Handlers die with this node, so an upgrade
	// never has to worry about unsubscribing from the global event bus.
	public event Action<int> WaveCleared;
	public event Action<Node3D> EnemyKilled;

	private readonly List<UpgradeDefinition> _owned = new();
	private readonly Dictionary<UpgradeDefinition, int> _stacks = new();

	public override void _Ready()
	{
		if (GameEvents.Instance == null)
			return;
		GameEvents.Instance.WaveCleared += OnWaveCleared;
		GameEvents.Instance.EnemyKilled += OnEnemyKilled;
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance == null)
			return;
		GameEvents.Instance.WaveCleared -= OnWaveCleared;
		GameEvents.Instance.EnemyKilled -= OnEnemyKilled;
	}

	private void OnWaveCleared(int waveNumber) => WaveCleared?.Invoke(waveNumber);
	private void OnEnemyKilled(Node3D enemy) => EnemyKilled?.Invoke(enemy);

	public int StacksOf(UpgradeDefinition upgrade) =>
		upgrade != null && _stacks.TryGetValue(upgrade, out int stacks) ? stacks : 0;

	/// <summary>True if the upgrade exists and is not yet at its MaxStacks.</summary>
	public bool CanGrant(UpgradeDefinition upgrade) =>
		upgrade != null && StacksOf(upgrade) < upgrade.MaxStacks;

	/// <summary>
	/// Up to count random, distinct upgrades from the pool that can still be taken.
	/// Returns fewer (possibly none) when the pool runs dry.
	/// </summary>
	public Godot.Collections.Array<UpgradeDefinition> RollChoices(int count)
	{
		var candidates = new List<UpgradeDefinition>();
		if (Pool != null)
		{
			foreach (UpgradeDefinition upgrade in Pool.Upgrades)
			{
				if (CanGrant(upgrade) && !candidates.Contains(upgrade))
					candidates.Add(upgrade);
			}
		}

		var choices = new Godot.Collections.Array<UpgradeDefinition>();
		while (choices.Count < count && candidates.Count > 0)
		{
			int index = (int)(GD.Randi() % (uint)candidates.Count);
			choices.Add(candidates[index]);
			candidates.RemoveAt(index);
		}
		return choices;
	}

	/// <summary>Gives the player one stack of an upgrade. Ignored if it is already maxed.</summary>
	public void Grant(UpgradeDefinition upgrade)
	{
		if (!CanGrant(upgrade))
		{
			GD.PushWarning($"PlayerUpgrades: cannot grant '{upgrade?.DisplayName}' (missing or at max stacks).");
			return;
		}

		int stacks = StacksOf(upgrade) + 1;
		_stacks[upgrade] = stacks;
		if (stacks == 1)
			_owned.Add(upgrade);

		upgrade.Apply(new UpgradeContext
		{
			Player = GetParent<PlayerController>(),
			Health = GetNode<HealthComponent>(HealthPath),
			WeaponMods = GetNodeOrNull<PlayerWeaponInput>(WeaponInputPath)?.SharedMods,
			Upgrades = this,
		}, stacks);

		EmitSignal(SignalName.UpgradeGranted, upgrade, stacks);
	}
}
