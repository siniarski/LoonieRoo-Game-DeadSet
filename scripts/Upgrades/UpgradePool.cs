using Godot;
using Godot.Collections;

namespace Deadset.Upgrades;

/// <summary>The set of upgrades the picker can offer.</summary>
[GlobalClass]
public partial class UpgradePool : Resource
{
	[Export] public Array<UpgradeDefinition> Upgrades { get; set; } = new();
}
