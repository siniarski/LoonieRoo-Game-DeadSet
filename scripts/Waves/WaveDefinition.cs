using Godot;
using Godot.Collections;

namespace Deadset.Waves;

/// <summary>Everything that spawns in one wave, plus how fast it trickles in.</summary>
[GlobalClass]
public partial class WaveDefinition : Resource
{
	[Export] public Array<WaveSpawn> Spawns { get; set; } = new();
	/// <summary>Never more than this many of the wave's enemies alive at once.</summary>
	[Export] public int MaxAlive { get; set; } = 12;
	/// <summary>Seconds between packs arriving.</summary>
	[Export] public float PackInterval { get; set; } = 2.5f;
}
