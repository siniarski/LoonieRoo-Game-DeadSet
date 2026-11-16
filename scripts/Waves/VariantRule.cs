using Deadset.Enemies;
using Godot;

namespace Deadset.Waves;

/// <summary>
/// When a variant starts turning up in waves, and how often. Each spawned enemy whose type
/// matches rolls its rules; the share grows a little each wave up to MaxShare.
/// </summary>
[GlobalClass]
public partial class VariantRule : Resource
{
	/// <summary>The enemy type it applies to (its EnemyStats DisplayName, e.g. "Wombat").</summary>
	[Export] public string Enemy { get; set; } = "";
	[Export] public EnemyVariant Variant { get; set; }
	[Export] public int FromWave { get; set; } = 4;
	[Export(PropertyHint.Range, "0,1")] public float FirstShare { get; set; } = 0.1f;
	[Export] public float ShareGrowthPerWave { get; set; } = 0.05f;
	[Export(PropertyHint.Range, "0,1")] public float MaxShare { get; set; } = 0.3f;
	/// <summary>At most one per pack, and only the first of the pack (a pack leader).</summary>
	[Export] public bool OnePerPack { get; set; }

	public float ShareFor(int wave) =>
		wave < FromWave ? 0f : Mathf.Min(FirstShare + ShareGrowthPerWave * (wave - FromWave), MaxShare);
}
