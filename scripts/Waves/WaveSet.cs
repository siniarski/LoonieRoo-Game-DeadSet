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

	[ExportGroup("Flankers")]
	/// <summary>
	/// From this wave on, some enemies path round to come at the player from behind, so no
	/// spot (the end of a lane) is fed from only one direction.
	/// </summary>
	[Export] public int FlankFromWave { get; set; } = 2;
	/// <summary>Share of enemies that flank in FlankFromWave, rising evenly to FlankerShare by FullFlankWave.</summary>
	[Export(PropertyHint.Range, "0,1")] public float FirstFlankerShare { get; set; } = 0.1f;
	/// <summary>Share of each wave's enemies that flank once ramped up (0.25 = one in four).</summary>
	[Export(PropertyHint.Range, "0,1")] public float FlankerShare { get; set; } = 0.25f;
	[Export] public int FullFlankWave { get; set; } = 6;

	public float FlankerShareFor(int wave)
	{
		if (wave < FlankFromWave)
			return 0f;
		float t = FullFlankWave > FlankFromWave ? Mathf.Clamp((float)(wave - FlankFromWave) / (FullFlankWave - FlankFromWave), 0f, 1f) : 1f;
		return Mathf.Lerp(FirstFlankerShare, FlankerShare, t);
	}

	[ExportGroup("Late waves get chunkier")]
	/// <summary>From this wave on, enemies get tougher and wider each wave.</summary>
	[Export] public int ChunkyFromWave { get; set; } = 6;
	/// <summary>Extra health per wave from ChunkyFromWave (0.1 = +10% a wave).</summary>
	[Export] public float HealthGrowthPerWave { get; set; } = 0.1f;
	[Export] public float MaxHealthMultiplier { get; set; } = 2f;
	/// <summary>Extra width per wave. Height barely changes, so heads stay at eye height.</summary>
	[Export] public float WidthGrowthPerWave { get; set; } = 0.03f;
	[Export] public float MaxWidth { get; set; } = 1.15f;

	public float HealthMultiplierFor(int wave) =>
		Mathf.Min(1f + HealthGrowthPerWave * Mathf.Max(wave - ChunkyFromWave + 1, 0), MaxHealthMultiplier);

	public float WidthFor(int wave) =>
		Mathf.Min(1f + WidthGrowthPerWave * Mathf.Max(wave - ChunkyFromWave + 1, 0), MaxWidth);
}
