using Godot;

namespace Deadset.Combat;

/// <summary>
/// Shared health for anything that can be hurt (dummies, enemies, the player).
/// Add as a child named "HealthComponent" so projectiles can find it.
/// </summary>
[GlobalClass]
public partial class HealthComponent : Node
{
	[Signal] public delegate void DamagedEventHandler(float amount, float remaining);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void HealthResetEventHandler();

	[Export] public float MaxHealth { get; set; } = 100f;

	public float Current { get; private set; }
	public bool IsDead => Current <= 0f;

	public override void _Ready()
	{
		Current = MaxHealth;
	}

	/// <summary>Applies damage. Returns true if this hit was the killing blow.</summary>
	public bool ApplyDamage(float amount)
	{
		if (IsDead || amount <= 0f)
			return false;

		Current = Mathf.Max(0f, Current - amount);
		EmitSignal(SignalName.Damaged, amount, Current);

		if (!IsDead)
			return false;

		EmitSignal(SignalName.Died);
		return true;
	}

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
