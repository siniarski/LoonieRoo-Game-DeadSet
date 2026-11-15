using Godot;
using Godot.Collections;

namespace Deadset.Waves;

/// <summary>The whole run: the list of waves and the breaks between them.</summary>
[GlobalClass]
public partial class WaveSet : Resource
{
	[Export] public Array<WaveDefinition> Waves { get; set; } = new();
	/// <summary>Seconds before wave 1.</summary>
	[Export] public float FirstWaveDelay { get; set; } = 6f;
	/// <summary>Trader break length between waves, in seconds.</summary>
	[Export] public float BreakDuration { get; set; } = 30f;
	/// <summary>After the last defined wave, it repeats with counts multiplied by this per extra wave.</summary>
	[Export] public float EndlessCountMultiplier { get; set; } = 1.2f;
	/// <summary>MaxAlive grows by this per extra wave after the list runs out.</summary>
	[Export] public int EndlessMaxAliveGrowth { get; set; } = 2;
	/// <summary>Hard cap on enemies alive at once, however far the endless waves go.</summary>
	[Export] public int MaxAliveCap { get; set; } = 200;
}
