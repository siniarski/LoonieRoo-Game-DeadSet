using Godot;

namespace Deadset.Waves;

/// <summary>One enemy type and how many of it appear in a wave.</summary>
[GlobalClass]
public partial class WaveSpawn : Resource
{
	[Export] public PackedScene Enemy { get; set; }
	[Export] public int Count { get; set; } = 10;
	/// <summary>How many of this type arrive together at one spawn point.</summary>
	[Export] public int PackSize { get; set; } = 4;
	/// <summary>0 = spread through the whole wave, 1 = only near the end.</summary>
	[Export(PropertyHint.Range, "0,1")] public float EarliestFraction { get; set; }
}
