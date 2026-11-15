using System.Collections.Generic;
using Godot;

namespace Deadset.ArtPreviews;

/// <summary>
/// Restyles a scene tree with a CelStyle, without touching the scenes on disk.
/// Every mesh and CSG shape under the root gets a toon ShaderMaterial built from its current
/// StandardMaterial3D (colour, emission, unshaded flag). The environment, sun, ink-line quad and
/// colour grade are swapped alongside. Clear() puts everything back.
/// Nodes that enter the tree later (spawned enemies, nails) are styled as they arrive.
/// </summary>
public partial class StyleApplier : Node
{
	private const string ShaderDir = "res://art_previews/shaders/";

	/// <summary>The tree to restyle. Empty = this node's parent.</summary>
	[Export] public NodePath RootPath { get; set; }
	/// <summary>Applied on ready, if set.</summary>
	[Export] public CelStyle InitialStyle { get; set; }

	/// <summary>The style in effect. For a style with a DuskTarget this is the blend at the current Dusk.</summary>
	public CelStyle Current { get; private set; }

	/// <summary>
	/// 0..1 shift towards the applied style's DuskTarget (ignored by styles without one).
	/// Meant to be driven from game flow, e.g. raised a step per wave. Each change rebuilds the
	/// styled materials, so step it per wave or every few seconds, not every frame.
	/// </summary>
	public float Dusk
	{
		get => _dusk;
		set
		{
			value = Mathf.Clamp(value, 0f, 1f);
			if (Mathf.IsEqualApprox(value, _dusk))
				return;
			_dusk = value;
			if (_baseStyle?.DuskTarget != null)
				Apply(_baseStyle);
		}
	}

	private float _dusk;
	private readonly Dictionary<(ulong, int), CelStyle> _blends = new();
	private CelStyle _baseStyle;

	private sealed class Saved
	{
		public Material Override;
		public Material[] Surfaces;
	}

	private sealed class Entry
	{
		public BaseMaterial3D Source;
		public ShaderMaterial Toon;
		public bool Enemy;
		/// <summary>The source is a per-instance override that game code animates (wombat glow).</summary>
		public bool Dynamic;
		public Color LastAlbedo;
		public float LastEnergy;
		public readonly List<GeometryInstance3D> Users = new();
	}

	private readonly Dictionary<GeometryInstance3D, Saved> _saved = new();
	private readonly Dictionary<(ulong, int, int), Entry> _entries = new();
	private readonly List<Entry> _dynamic = new();
	private readonly List<GeometryInstance3D> _pending = new();
	private bool _flushQueued;
	private double _pruneTimer;

	private Shader _toonShader;
	private Shader _outlineShader;
	private Node _root;

	private WorldEnvironment _worldEnvironment;
	private Environment _originalEnvironment;
	private DirectionalLight3D _sun;
	private Color _sunColor;
	private float _sunEnergy;
	private Vector3 _sunRotation;
	private Viewport.ScreenSpaceAAEnum _originalAa;

	private ShaderMaterial _skyMaterial;
	private MeshInstance3D _edgeQuad;
	private CanvasLayer _gradeLayer;

	public override void _Ready()
	{
		_toonShader = GD.Load<Shader>(ShaderDir + "cel_toon.gdshader");
		_outlineShader = GD.Load<Shader>(ShaderDir + "cel_outline.gdshader");
		_root = RootPath == null || RootPath.IsEmpty ? GetParent() : GetNode(RootPath);
		GetTree().NodeAdded += OnNodeAdded;
		if (InitialStyle != null)
			CallDeferred(MethodName.Apply, InitialStyle);
	}

	public override void _ExitTree()
	{
		GetTree().NodeAdded -= OnNodeAdded;
	}

	/// <summary>Switches to a style. Null returns to the untouched look.</summary>
	public void Apply(CelStyle style)
	{
		Clear();
		if (style == null)
			return;
		_baseStyle = style;
		style = style.DuskTarget != null ? Blended(style) : style;
		Current = style;

		ApplyWorld(style);
		ApplyEdges(style);
		ApplyGrade(style);
		StyleTree(_root);
	}

	/// <summary>
	/// The blend for the current Dusk, in steps of 0.01. Blends are kept for the life of the
	/// applier: re-visiting a dusk value is free, and no half-used style is ever collected mid-run.
	/// </summary>
	private CelStyle Blended(CelStyle style)
	{
		var key = (style.GetInstanceId(), Mathf.RoundToInt(_dusk * 100f));
		if (!_blends.TryGetValue(key, out CelStyle blend))
		{
			blend = CelStyle.Blend(style, style.DuskTarget, key.Item2 / 100f);
			_blends[key] = blend;
		}
		return blend;
	}

	/// <summary>Restores every material, the environment, the sun and the viewport.</summary>
	public void Clear()
	{
		if (Current == null)
			return;
		Current = null;
		_baseStyle = null;

		foreach ((GeometryInstance3D node, Saved saved) in _saved)
		{
			if (!IsInstanceValid(node))
				continue;
			node.MaterialOverride = saved.Override;
			if (node is MeshInstance3D mesh && saved.Surfaces != null)
				for (int i = 0; i < saved.Surfaces.Length && i < mesh.GetSurfaceOverrideMaterialCount(); i++)
					mesh.SetSurfaceOverrideMaterial(i, saved.Surfaces[i]);
		}
		_saved.Clear();
		_entries.Clear();
		_dynamic.Clear();
		_pending.Clear();

		if (IsInstanceValid(_worldEnvironment))
			_worldEnvironment.Environment = _originalEnvironment;
		if (IsInstanceValid(_sun))
		{
			_sun.LightColor = _sunColor;
			_sun.LightEnergy = _sunEnergy;
			_sun.Rotation = _sunRotation;
		}
		_worldEnvironment = null;
		_sun = null;
		_skyMaterial = null;
		GetViewport().ScreenSpaceAA = _originalAa;

		_edgeQuad?.QueueFree();
		_edgeQuad = null;
		_gradeLayer?.QueueFree();
		_gradeLayer = null;
	}

	// ---- World: environment, sun, anti-aliasing ----------------------------------------

	private void ApplyWorld(CelStyle style)
	{
		_worldEnvironment = FindFirst<WorldEnvironment>(_root);
		if (_worldEnvironment != null)
		{
			_originalEnvironment = _worldEnvironment.Environment;
			if (style.Environment != null)
			{
				_worldEnvironment.Environment = style.Environment;
				if (style.Environment.Sky?.SkyMaterial is ShaderMaterial sky && sky.GetShaderParameter("flat_bands").AsBool())
					_skyMaterial = sky;
			}
		}

		_sun = FindFirst<DirectionalLight3D>(_root);
		if (_sun != null)
		{
			_sunColor = _sun.LightColor;
			_sunEnergy = _sun.LightEnergy;
			_sunRotation = _sun.Rotation;
			if (style.OverrideSun)
			{
				_sun.LightColor = style.SunColor;
				_sun.LightEnergy = style.SunEnergy;
			}
			if (style.OverrideSunRotation)
				_sun.RotationDegrees = style.SunRotationDegrees;
		}

		_originalAa = GetViewport().ScreenSpaceAA;
		if (style.Fxaa)
			GetViewport().ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa;
	}

	private void ApplyEdges(CelStyle style)
	{
		if (!style.EdgesEnabled)
			return;
		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderDir + "cel_edges.gdshader") };
		material.SetShaderParameter("ink_color", style.InkColor);
		material.SetShaderParameter("thickness", style.InkThickness);
		material.SetShaderParameter("depth_threshold", style.InkDepthThreshold);
		material.SetShaderParameter("normal_threshold", style.InkNormalThreshold);
		material.SetShaderParameter("normal_fade_start", style.InkNormalFadeStart);
		material.SetShaderParameter("normal_fade_end", style.InkNormalFadeEnd);
		// Draw after the sparks and other late geometry.
		material.RenderPriority = 100;

		_edgeQuad = new MeshInstance3D
		{
			Name = "CelEdgeQuad",
			Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			// The vertex shader pins it to the screen, so it must never be culled.
			CustomAabb = new Aabb(new Vector3(-10000f, -10000f, -10000f), new Vector3(20000f, 20000f, 20000f)),
		};
		AddChild(_edgeQuad);
	}

	private void ApplyGrade(CelStyle style)
	{
		if (!style.GradeEnabled)
			return;
		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderDir + "cel_grade.gdshader") };
		material.SetShaderParameter("saturation", style.GradeSaturation);
		material.SetShaderParameter("contrast", style.GradeContrast);
		material.SetShaderParameter("brightness", style.GradeBrightness);
		material.SetShaderParameter("shadow_tint", style.ShadowTint);
		material.SetShaderParameter("highlight_tint", style.HighlightTint);
		material.SetShaderParameter("tint_amount", style.TintAmount);
		material.SetShaderParameter("paper_color", style.PaperColor);
		material.SetShaderParameter("black_lift", style.BlackLift);
		material.SetShaderParameter("posterize_steps", style.PosterizeSteps);
		material.SetShaderParameter("grain", style.Grain);
		material.SetShaderParameter("vignette", style.Vignette);

		// Below the HUD (layer 1), above the 3D view: the HUD keeps its own colours.
		_gradeLayer = new CanvasLayer { Name = "CelGrade", Layer = -1 };
		var rect = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_gradeLayer.AddChild(rect);
		AddChild(_gradeLayer);
	}

	// ---- Materials ----------------------------------------------------------------------

	private void OnNodeAdded(Node node)
	{
		if (Current == null || node is not GeometryInstance3D geometry || !IsStyleable(geometry))
			return;
		// Wait until the end of the frame: the node's owner may still set up its own
		// materials in _Ready (the wombat duplicates its glow material there).
		_pending.Add(geometry);
		if (_flushQueued)
			return;
		_flushQueued = true;
		CallDeferred(MethodName.FlushPending);
	}

	private void FlushPending()
	{
		_flushQueued = false;
		foreach (GeometryInstance3D geometry in _pending)
			if (IsInstanceValid(geometry) && geometry.IsInsideTree() && Current != null)
				StyleNode(geometry);
		_pending.Clear();
	}

	private bool IsStyleable(GeometryInstance3D node) =>
		(node is MeshInstance3D || node is CsgPrimitive3D) && node != _edgeQuad
		&& (_root == node || _root.IsAncestorOf(node));

	private void StyleTree(Node node)
	{
		if (node == this)
			return;
		if (node is GeometryInstance3D geometry && IsStyleable(geometry))
			StyleNode(geometry);
		foreach (Node child in node.GetChildren())
			StyleTree(child);
	}

	private void StyleNode(GeometryInstance3D node)
	{
		if (_saved.ContainsKey(node))
			return;

		// 0 world, 1 enemy, 2 the player's weapon.
		int kind = IsEnemyPart(node) ? 1 : IsWeaponPart(node) ? 2 : 0;
		int shape = ShapeOf(node);
		bool hull = Current.HullEnabled && !IsSlab(node)
			&& (kind == 1 || (kind == 2 ? Current.HullOnWeapon : Current.HullOnWorld));
		var saved = new Saved { Override = node.MaterialOverride };

		if (node.MaterialOverride != null)
		{
			if (Convert(node.MaterialOverride, shape, kind, hull, true, node) is { } toon)
				node.MaterialOverride = toon;
			else
				return;
		}
		else if (node is CsgPrimitive3D csg)
		{
			if (Convert(csg.Get("material").As<Material>(), shape, kind, hull, false, node) is { } toon)
				node.MaterialOverride = toon;
			else
				return;
		}
		else if (node is MeshInstance3D mesh && mesh.Mesh != null)
		{
			int count = mesh.GetSurfaceOverrideMaterialCount();
			saved.Surfaces = new Material[count];
			bool any = false;
			for (int i = 0; i < count; i++)
			{
				saved.Surfaces[i] = mesh.GetSurfaceOverrideMaterial(i);
				Material source = saved.Surfaces[i] ?? mesh.Mesh.SurfaceGetMaterial(i);
				if (Convert(source, shape, kind, hull, false, node) is { } toon)
				{
					mesh.SetSurfaceOverrideMaterial(i, toon);
					any = true;
				}
			}
			if (!any)
				return;
		}
		else
		{
			return;
		}
		_saved[node] = saved;
	}

	/// <summary>Toon material for a source material, shared by every mesh that uses the same source.</summary>
	private ShaderMaterial Convert(Material material, int shape, int kind, bool hull, bool dynamic, GeometryInstance3D user)
	{
		if (material is not BaseMaterial3D source || source.Transparency != BaseMaterial3D.TransparencyEnum.Disabled)
			return null;

		bool unshaded = source.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded;
		// Flat-colour parts (eyes, flash) stay line-free so the colour is not swallowed at range.
		int hullShape = hull && !unshaded ? shape : -1;
		var key = (source.GetInstanceId(), hullShape, kind);
		if (_entries.TryGetValue(key, out Entry entry))
		{
			entry.Users.Add(user);
			return entry.Toon;
		}

		entry = new Entry
		{
			Source = source,
			Toon = new ShaderMaterial { Shader = _toonShader },
			Enemy = kind == 1,
			Dynamic = dynamic,
		};
		entry.Users.Add(user);
		SetStyleParameters(entry.Toon, kind);
		CopySource(entry);
		if (hullShape >= 0)
			entry.Toon.NextPass = MakeHull(source, hullShape, kind == 1);

		_entries[key] = entry;
		if (dynamic)
			_dynamic.Add(entry);
		return entry.Toon;
	}

	private void SetStyleParameters(ShaderMaterial toon, int kind)
	{
		CelStyle s = Current;
		bool enemy = kind == 1;
		float rim = !enemy && !s.RimOnWorld ? 0f : s.RimStrength * (enemy ? s.EnemyRimMultiplier : 1f);
		toon.SetShaderParameter("bands", s.Bands);
		toon.SetShaderParameter("lit_threshold", s.LitThreshold);
		toon.SetShaderParameter("mid_threshold", s.MidThreshold);
		toon.SetShaderParameter("mid_level", s.MidLevel);
		toon.SetShaderParameter("band_softness", s.BandSoftness);
		toon.SetShaderParameter("rim_color", s.RimColor);
		toon.SetShaderParameter("rim_strength", rim);
		toon.SetShaderParameter("self_light", enemy ? s.EnemySelfLight : kind == 2 ? s.WeaponSelfLight : 0f);
		toon.SetShaderParameter("rim_width", s.RimWidth);
		toon.SetShaderParameter("rim_softness", s.RimSoftness);
		toon.SetShaderParameter("far_grow", s.UnshadedFarGrow);
		toon.SetShaderParameter("shadow_pattern", s.ShadowPattern);
		toon.SetShaderParameter("pattern_scale", s.PatternScale);
		toon.SetShaderParameter("pattern_strength", s.PatternStrength);
		toon.SetShaderParameter("hatch_darkness", s.HatchDarkness);
		toon.SetShaderParameter("fog_color", s.FogColor);
		toon.SetShaderParameter("fog_start", s.FogStart);
		toon.SetShaderParameter("fog_end", s.FogEnd);
		toon.SetShaderParameter("fog_max", s.FogMax * (enemy ? s.EnemyFogScale : 1f));
		toon.SetShaderParameter("fog_bands", s.FogBands);
		if (s.Environment != null)
		{
			Color ambient = s.Environment.AmbientLightColor * s.Environment.AmbientLightEnergy;
			toon.SetShaderParameter("ambient_light", ambient);
		}
	}

	/// <summary>Copies the colours that come from the source material (and may change at runtime).</summary>
	private void CopySource(Entry entry)
	{
		CelStyle s = Current;
		BaseMaterial3D source = entry.Source;
		ShaderMaterial toon = entry.Toon;
		bool unshaded = source.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded;

		Color albedo = unshaded ? source.AlbedoColor * s.UnshadedBoost : Grade(source.AlbedoColor, entry.Enemy);
		if (source.EmissionEnabled && !unshaded)
			albedo = new Color(albedo.R * s.GlowAlbedo, albedo.G * s.GlowAlbedo, albedo.B * s.GlowAlbedo, albedo.A);
		toon.SetShaderParameter("albedo", albedo);
		toon.SetShaderParameter("glow_keep_hue", s.GlowKeepHue ? 1f : 0f);
		toon.SetShaderParameter("unshaded", unshaded ? 1f : 0f);
		toon.SetShaderParameter("emission", source.EmissionEnabled ? source.Emission : Colors.Black);
		toon.SetShaderParameter("emission_energy",
			source.EmissionEnabled ? source.EmissionEnergyMultiplier * s.EmissionBoost : 0f);

		if (source.AlbedoTexture != null && source.Uv1Triplanar)
		{
			toon.SetShaderParameter("albedo_tex", source.AlbedoTexture);
			toon.SetShaderParameter("tex_amount", s.TextureAmount);
			toon.SetShaderParameter("tex_scale", source.Uv1Scale.X * s.TextureScale);
			toon.SetShaderParameter("tex_steps", s.TextureSteps);
		}

		entry.LastAlbedo = source.AlbedoColor;
		entry.LastEnergy = source.EmissionEnergyMultiplier;
	}

	private Color Grade(Color color, bool enemy)
	{
		color.ToHsv(out float h, out float sat, out float v);
		sat = Mathf.Clamp(sat * Current.Saturation * (enemy ? Current.EnemySaturation : 1f), 0f, 1f);
		v = Mathf.Max(v * Current.Value, Mathf.Min(Current.MinValue, 1f)) * (enemy ? Current.EnemyValue : 1f);
		return Color.FromHsv(h, sat, Mathf.Min(v, 1f), color.A);
	}

	private ShaderMaterial MakeHull(BaseMaterial3D source, int shape, bool enemy)
	{
		CelStyle s = Current;
		Color color = enemy && s.UseEnemyHullColor ? s.EnemyHullColor : s.HullColor;
		float glow = 0f;
		if (s.HullSurfaceTint > 0f)
		{
			source.AlbedoColor.ToHsv(out float h, out float sat, out float _);
			Color dark = Color.FromHsv(h, Mathf.Min(sat * 1.3f + 0.1f, 1f), 0.22f);
			color = color.Lerp(dark, s.HullSurfaceTint);
		}
		if (s.HullGlowFromEmission && source.EmissionEnabled)
		{
			color = source.Emission;
			glow = s.HullGlowEnergy;
		}

		var hull = new ShaderMaterial { Shader = _outlineShader };
		hull.SetShaderParameter("outline_color", color);
		hull.SetShaderParameter("width", s.HullWidth);
		hull.SetShaderParameter("thin_distance", s.HullThinDistance);
		hull.SetShaderParameter("min_width", s.HullMinWidth);
		hull.SetShaderParameter("shape", shape);
		hull.SetShaderParameter("glow", glow);
		return hull;
	}

	private static int ShapeOf(GeometryInstance3D node) => node switch
	{
		CsgBox3D => 1,
		CsgCylinder3D => 2,
		MeshInstance3D { Mesh: BoxMesh } => 1,
		MeshInstance3D { Mesh: CylinderMesh } => 2,
		_ => 0,
	};

	/// <summary>Floor-like slabs get no outline shell: it would only draw a line around the whole arena.</summary>
	private static bool IsSlab(GeometryInstance3D node)
	{
		Vector3 size = node is CsgBox3D box ? box.Size : node.GetAabb().Size;
		return Mathf.Min(size.X, size.Z) > 20f;
	}

	private static bool IsEnemyPart(Node node)
	{
		for (Node n = node; n != null; n = n.GetParent())
			if (n.IsInGroup("enemies"))
				return true;
		return false;
	}

	private static bool IsWeaponPart(Node node)
	{
		for (Node n = node; n != null; n = n.GetParent())
			if (n is Camera3D)
				return true;
		return false;
	}

	private static T FindFirst<T>(Node node) where T : Node
	{
		if (node is T match)
			return match;
		foreach (Node child in node.GetChildren())
			if (FindFirst<T>(child) is { } found)
				return found;
		return null;
	}

	// ---- Per frame ----------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (Current == null)
			return;

		// Flat sky bands need to know which way the camera faces.
		if (_skyMaterial != null && GetViewport().GetCamera3D() is { } camera)
		{
			Vector3 forward = -camera.GlobalBasis.Z;
			forward.Y = 0f;
			if (forward.LengthSquared() > 0.0001f)
				_skyMaterial.SetShaderParameter("view_forward", forward.Normalized());
		}

		// Follow materials that game code animates (the wombat's wind-up glow).
		foreach (Entry entry in _dynamic)
			if (entry.Source.AlbedoColor != entry.LastAlbedo || entry.Source.EmissionEnergyMultiplier != entry.LastEnergy)
				CopySource(entry);

		_pruneTimer += delta;
		if (_pruneTimer < 2.0)
			return;
		_pruneTimer = 0.0;
		Prune();
	}

	/// <summary>Drops bookkeeping for nodes that have been freed (dead enemies, spent nails).</summary>
	private void Prune()
	{
		var deadNodes = new List<GeometryInstance3D>();
		foreach (GeometryInstance3D node in _saved.Keys)
			if (!IsInstanceValid(node))
				deadNodes.Add(node);
		foreach (GeometryInstance3D node in deadNodes)
			_saved.Remove(node);

		var deadKeys = new List<(ulong, int, int)>();
		foreach (((ulong, int, int) key, Entry entry) in _entries)
		{
			entry.Users.RemoveAll(user => !IsInstanceValid(user));
			if (entry.Dynamic && entry.Users.Count == 0)
				deadKeys.Add(key);
		}
		foreach ((ulong, int, int) key in deadKeys)
		{
			_dynamic.Remove(_entries[key]);
			_entries.Remove(key);
		}
	}
}
