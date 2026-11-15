using Godot;

namespace Deadset.Upgrades;

/// <summary>
/// One player upgrade, defined as data. Most upgrades are just a .tres of an existing
/// subclass (stat change, heal on event...); only a genuinely new kind of effect needs
/// a new subclass. Nothing here requires touching the player class.
/// </summary>
[GlobalClass]
public abstract partial class UpgradeDefinition : Resource
{
	/// <summary>Stable identifier, for saves and debugging.</summary>
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "Upgrade";
	/// <summary>One short line for the picker card.</summary>
	[Export] public string Description { get; set; } = "";
	[Export] public Color AccentColor { get; set; } = new(1f, 0.8f, 0.2f);
	/// <summary>How many times this can be taken.</summary>
	[Export(PropertyHint.Range, "1,10")] public int MaxStacks { get; set; } = 1;

	/// <summary>
	/// Called once each time the upgrade is granted. Stacks is the new total (1 on the
	/// first pick), so each call should add one stack's worth of effect.
	/// </summary>
	public abstract void Apply(UpgradeContext context, int stacks);
}
