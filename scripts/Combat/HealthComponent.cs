using System;
using System.Collections.Generic;
using Godot;

namespace Deadset.Combat;

/// <summary>
/// Shared health for anything that can be hurt (dummies, enemies, the player).
/// Add as a child named "HealthComponent" so projectiles can find it.
/// Armour (player only in practice) soaks damage before health. It starts at zero,
/// so anything that never gains armour behaves exactly as if it had none.
/// </summary>
[GlobalClass]
public partial class HealthComponent : Node
{
	/// <summary>
	/// Took a hit. Amount is the total damage after modifiers, including the part
	/// armour absorbed, so feedback fires even when only armour was lost.
	/// Remaining is the health left.
	/// </summary>
	[Signal] public delegate void DamagedEventHandler(float amount, float remaining);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void HealthResetEventHandler();
	/// <summary>Health was restored. Amount is what was actually gained.</summary>
	[Signal] public delegate void HealedEventHandler(float amount, float current);
	[Signal] public delegate void ArmourChangedEventHandler(float armour, float maxArmour);
	/// <summary>
	/// A hit that came from somewhere (an enemy's position), emitted right after Damaged.
	/// The HUD uses it to point at whoever is hurting you.
	/// </summary>
	[Signal] public delegate void HitFromEventHandler(Vector3 sourcePosition, float amount);

	[Export] public float MaxHealth { get; set; } = 100f;
	[Export] public float MaxArmour { get; set; } = 100f;
	/// <summary>Share of each hit that armour takes while there is armour left (1 = all of it).</summary>
	[Export(PropertyHint.Range, "0,1")] public float ArmourAbsorption { get; set; } = 1f;

	public float Current { get; private set; }
	/// <summary>
	/// True when the latest hit landed on a weak spot (set by the weapon just before it applies
	/// damage). Enemies read it on death, so a head-shot kill can pop the head off.
	/// </summary>
	public bool LastHitWeakSpot { get; set; }
	public float Armour { get; private set; }
	public bool IsDead => Current <= 0f;

	private readonly List<Func<float, float>> _damageModifiers = new();

	public override void _Ready()
	{
		Current = MaxHealth;
	}

	/// <summary>
	/// Registers a function that rewrites incoming damage (before armour). Upgrades use
	/// this for resistances; this component knows nothing about who is asking.
	/// </summary>
	public void AddDamageModifier(Func<float, float> modifier) => _damageModifiers.Add(modifier);

	public void RemoveDamageModifier(Func<float, float> modifier) => _damageModifiers.Remove(modifier);

	/// <summary>Applies damage from a known position (an enemy). Returns true if this hit was the killing blow.</summary>
	public bool ApplyDamage(float amount, Vector3 sourcePosition) => ApplyDamage(amount, sourcePosition, true);

	/// <summary>Applies damage. Returns true if this hit was the killing blow.</summary>
	public bool ApplyDamage(float amount) => ApplyDamage(amount, Vector3.Zero, false);

	private bool ApplyDamage(float amount, Vector3 sourcePosition, bool hasSource)
	{
		if (IsDead || amount <= 0f)
			return false;

		foreach (Func<float, float> modifier in _damageModifiers)
			amount = modifier(amount);
		if (amount <= 0f)
			return false;

		float toHealth = amount;
		if (Armour > 0f)
		{
			float absorbed = Mathf.Min(Armour, amount * ArmourAbsorption);
			Armour -= absorbed;
			toHealth -= absorbed;
			EmitSignal(SignalName.ArmourChanged, Armour, MaxArmour);
		}

		Current = Mathf.Max(0f, Current - toHealth);
		EmitSignal(SignalName.Damaged, amount, Current);
		if (hasSource)
			EmitSignal(SignalName.HitFrom, sourcePosition, amount);

		if (!IsDead)
			return false;

		EmitSignal(SignalName.Died);
		return true;
	}

	/// <summary>Restores health up to MaxHealth. Does nothing when dead.</summary>
	public void Heal(float amount)
	{
		if (IsDead || amount <= 0f)
			return;

		float before = Current;
		Current = Mathf.Min(MaxHealth, Current + amount);
		if (Current > before)
			EmitSignal(SignalName.Healed, Current - before, Current);
	}

	/// <summary>Adds armour, clamped to MaxArmour.</summary>
	public void AddArmour(float amount)
	{
		float before = Armour;
		Armour = Mathf.Clamp(Armour + amount, 0f, MaxArmour);
		if (!Mathf.IsEqualApprox(Armour, before))
			EmitSignal(SignalName.ArmourChanged, Armour, MaxArmour);
	}

	/// <summary>Back to full health. Armour is left as it is.</summary>
	public void ResetHealth()
	{
		Current = MaxHealth;
		EmitSignal(SignalName.HealthReset);
	}

	/// <summary>Finds the HealthComponent on a hit collider (or its parent), if any.</summary>
	public static HealthComponent FindOn(Node node)
	{
		if (node == null)
			return null;
		return node.GetNodeOrNull("HealthComponent") as HealthComponent
			?? node.GetParent()?.GetNodeOrNull("HealthComponent") as HealthComponent;
	}
}
