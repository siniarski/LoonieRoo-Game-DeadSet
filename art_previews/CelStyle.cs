using Godot;

namespace Deadset.ArtPreviews;

/// <summary>
/// One cel-shaded art direction as data: lighting bands, outlines, ink lines, fog, sky and grade.
/// StyleApplier reads this and restyles a scene tree at runtime. Adding a direction is a new .tres.
/// </summary>
[GlobalClass]
public partial class CelStyle : Resource
{
	[Export] public string DisplayName { get; set; } = "Style";

	[ExportGroup("World")]
	/// <summary>Replaces the WorldEnvironment's environment (sky, ambient, glow, tonemap).</summary>
	[Export] public Environment Environment { get; set; }
	[Export] public bool OverrideSun { get; set; } = true;
	[Export] public Color SunColor { get; set; } = new(1f, 0.88f, 0.72f);
	[Export] public float SunEnergy { get; set; } = 1.2f;
	[Export] public bool OverrideSunRotation { get; set; }
	[Export] public Vector3 SunRotationDegrees { get; set; } = new(-51.6f, 34.4f, 0f);

	[ExportGroup("Palette")]
	/// <summary>Multiplies the saturation of every surface colour.</summary>
	[Export] public float Saturation { get; set; } = 1f;
	/// <summary>Multiplies the brightness of every surface colour.</summary>
	[Export] public float Value { get; set; } = 1f;
	/// <summary>Lifts very dark surface colours to at least this brightness, so dark bodies keep a readable lit side.</summary>
	[Export] public float MinValue { get; set; }
	/// <summary>Multiplies the emission strength of glowing parts (mutation patches, sights).</summary>
	[Export] public float EmissionBoost { get; set; } = 1f;
	/// <summary>How much of the lit surface colour glowing parts keep (0 = a flat glow, the cel look).</summary>
	[Export] public float GlowAlbedo { get; set; }
	/// <summary>Bright glows keep their hue instead of clipping towards yellow-white.</summary>
	[Export] public bool GlowKeepHue { get; set; } = true;
	/// <summary>Brightness of unshaded parts (eyes, muzzle flash). Above 1 they bloom.</summary>
	[Export] public float UnshadedBoost { get; set; } = 1f;
	/// <summary>How much of the source noise texture shows on the floor and walls (0-1).</summary>
	[Export] public float TextureAmount { get; set; } = 1f;
	/// <summary>Size of the texture pattern: below 1 the blotches get bigger.</summary>
	[Export] public float TextureScale { get; set; } = 1f;
	/// <summary>Cuts that texture into this many flat steps (0 = smooth).</summary>
	[Export] public float TextureSteps { get; set; } = 2f;

	/// <summary>Extra brightness multiplier for enemy surfaces (below 1 pushes them towards silhouettes).</summary>
	[Export] public float EnemyValue { get; set; } = 1f;

	[ExportGroup("Light bands")]
	[Export(PropertyHint.Range, "2,3")] public int Bands { get; set; } = 2;
	[Export] public float LitThreshold { get; set; } = 0.1f;
	[Export] public float MidThreshold { get; set; } = -0.35f;
	[Export] public float MidLevel { get; set; } = 0.45f;
	[Export] public float BandSoftness { get; set; } = 0.01f;

	[ExportGroup("Rim light")]
	[Export] public Color RimColor { get; set; } = Colors.White;
	[Export] public float RimStrength { get; set; }
	[Export] public float RimWidth { get; set; } = 0.35f;
	/// <summary>Rim on world geometry and the weapon too. Off by default: flat faces light up whole at grazing angles.</summary>
	[Export] public bool RimOnWorld { get; set; }
	/// <summary>Rim strength on enemies, as a multiple of RimStrength.</summary>
	[Export] public float EnemyRimMultiplier { get; set; } = 1f;

	[ExportGroup("Shadow pattern")]
	/// <summary>0 none, 1 halftone dots on the shadow edge, 2 dots plus hatching inside the shadow.</summary>
	[Export(PropertyHint.Range, "0,2")] public int ShadowPattern { get; set; }
	[Export] public float PatternScale { get; set; } = 6f;
	[Export] public float PatternStrength { get; set; } = 0.5f;
	[Export] public float HatchDarkness { get; set; } = 0.45f;

	[ExportGroup("Fog")]
	/// <summary>Strongest fog blend (0 = no fog). Replaces the environment fog for styled surfaces.</summary>
	[Export] public float FogMax { get; set; }
	[Export] public Color FogColor { get; set; } = new(0.8f, 0.55f, 0.38f);
	[Export] public float FogStart { get; set; } = 10f;
	[Export] public float FogEnd { get; set; } = 60f;
	/// <summary>0 = smooth, otherwise the number of flat fog steps.</summary>
	[Export] public float FogBands { get; set; }

	/// <summary>Fog strength on enemies relative to the world (below 1 keeps them punchier than their backdrop).</summary>
	[Export] public float EnemyFogScale { get; set; } = 1f;

	[ExportGroup("Hull outlines")]
	[Export] public bool HullEnabled { get; set; }
	[Export] public Color HullColor { get; set; } = Colors.Black;
	/// <summary>Blends the outline towards a dark version of the surface colour (coloured line art).</summary>
	[Export] public float HullSurfaceTint { get; set; }
	[Export] public bool UseEnemyHullColor { get; set; }
	[Export] public Color EnemyHullColor { get; set; } = Colors.Black;
	/// <summary>Glowing parts get an outline in their own glow colour.</summary>
	[Export] public bool HullGlowFromEmission { get; set; }
	[Export] public float HullGlowEnergy { get; set; } = 1f;
	/// <summary>Pixels at a 900 px tall viewport.</summary>
	[Export] public float HullWidth { get; set; } = 3f;
	[Export] public float HullThinDistance { get; set; } = 10f;
	[Export] public float HullMinWidth { get; set; } = 1.2f;
	/// <summary>Outline world geometry too (cover, walls), not only characters and the weapon.</summary>
	[Export] public bool HullOnWorld { get; set; } = true;

	[ExportGroup("Ink lines (screen space)")]
	[Export] public bool EdgesEnabled { get; set; }
	[Export] public Color InkColor { get; set; } = new(0.05f, 0.03f, 0.02f);
	[Export] public float InkThickness { get; set; } = 1.5f;
	[Export] public float InkDepthThreshold { get; set; } = 0.06f;
	[Export] public float InkNormalThreshold { get; set; } = 0.35f;
	[Export] public float InkNormalFadeStart { get; set; } = 12f;
	[Export] public float InkNormalFadeEnd { get; set; } = 30f;

	[ExportGroup("Grade")]
	[Export] public bool GradeEnabled { get; set; }
	[Export] public float GradeSaturation { get; set; } = 1f;
	[Export] public float GradeContrast { get; set; } = 1f;
	[Export] public float GradeBrightness { get; set; } = 1f;
	[Export] public Color ShadowTint { get; set; } = new(0.5f, 0.5f, 0.5f);
	[Export] public Color HighlightTint { get; set; } = new(0.5f, 0.5f, 0.5f);
	[Export] public float TintAmount { get; set; }
	[Export] public Color PaperColor { get; set; } = Colors.White;
	[Export] public float BlackLift { get; set; }
	[Export] public float PosterizeSteps { get; set; }
	[Export] public float Grain { get; set; }
	[Export] public float Vignette { get; set; }
	/// <summary>Smooths the ink and outline edges, which MSAA does not reach.</summary>
	[Export] public bool Fxaa { get; set; }
}
