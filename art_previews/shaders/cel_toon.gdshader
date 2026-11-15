// Cel-shaded surface. One shader for every direction; the look comes from the uniforms,
// which StyleApplier fills from a CelStyle resource plus the source StandardMaterial3D.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, specular_disabled;

// --- From the source material -------------------------------------------------------
uniform vec4 albedo : source_color = vec4(1.0);
uniform vec3 emission : source_color = vec3(0.0);
uniform float emission_energy = 0.0;
/// 1 = keep the glow colour's hue when it gets brighter than the screen can show, instead of
/// letting it clip towards yellow-white. Very bright glows still wash out a little, so a
/// flare-up (the wombat's wind-up) reads as a flare-up.
uniform float glow_keep_hue = 1.0;
/// Adds this much of the surface colour as its own light, so the shadow side keeps its hue
/// (the orange nail gun, enemy bodies) instead of sinking into the ambient colour.
uniform float self_light = 0.0;
/// Unshaded parts (the red eyes) swell by this many metres per metre of distance beyond 6 m,
/// so they stay more than a pixel wide at range without changing up close.
uniform float far_grow = 0.0;
/// 1 = the source was unshaded (eyes, muzzle flash): draw the flat colour, ignore lights.
uniform float unshaded = 0.0;
uniform sampler2D albedo_tex : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float tex_amount = 0.0;
uniform float tex_scale = 0.25;
/// Quantises the texture into this many flat steps (0 = leave it smooth).
uniform float tex_steps = 0.0;

// --- From the style ------------------------------------------------------------------
uniform int bands = 2;
/// N.L above this is fully lit.
uniform float lit_threshold = 0.1;
/// Three-band only: N.L above this gets the mid tone.
uniform float mid_threshold = -0.35;
uniform float mid_level = 0.45;
uniform float band_softness = 0.01;
uniform vec3 rim_color : source_color = vec3(1.0);
uniform float rim_strength = 0.0;
uniform float rim_width = 0.35;
/// Width of the rim's inner fade. Small = a crisp line, large = a soft backlight.
uniform float rim_softness = 0.06;
/// 0 none, 1 halftone dots on the terminator, 2 dots plus hatching inside the shadow.
uniform int shadow_pattern = 0;
uniform float pattern_scale = 6.0;
uniform float pattern_strength = 0.5;
uniform float hatch_darkness = 0.45;
/// The environment's ambient light (colour times energy); only the hatching needs it.
uniform vec3 ambient_light : source_color = vec3(0.3);
uniform vec3 fog_color : source_color = vec3(0.8, 0.55, 0.38);
uniform float fog_start = 10.0;
uniform float fog_end = 60.0;
uniform float fog_max = 0.0;
/// 0 = smooth distance fog, otherwise this many flat steps.
uniform float fog_bands = 0.0;
/// How much glowing parts cut through the fog (keeps heads readable at range).
uniform float fog_glow_cut = 0.7;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	if (unshaded > 0.5 && far_grow > 0.0) {
		float dist = length((MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz);
		VERTEX += NORMAL * far_grow * clamp(dist - 6.0, 0.0, 24.0);
	}
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

float posterize(float v, float steps) {
	return floor(v * steps + 0.5) / steps;
}

float dots(vec2 frag, float scale) {
	// 45 degree halftone screen; returns 0..1 with 0.5 as the mean.
	mat2 rot = mat2(vec2(0.7071, -0.7071), vec2(0.7071, 0.7071));
	vec2 p = rot * frag / scale;
	vec2 c = fract(p) - 0.5;
	return clamp(length(c) * 1.6, 0.0, 1.0);
}

float hatch(vec2 frag, float scale) {
	float v = fract((frag.x + frag.y) / (scale * 1.4));
	return smoothstep(0.30, 0.38, v) - smoothstep(0.62, 0.70, v);
}

void fragment() {
	vec3 base = albedo.rgb;
	if (tex_amount > 0.0) {
		// World-space triplanar, like the source floor and wall materials.
		vec3 w = pow(abs(world_normal), vec3(4.0));
		w /= (w.x + w.y + w.z);
		vec3 p = world_pos * tex_scale;
		float t = texture(albedo_tex, p.zy).r * w.x + texture(albedo_tex, p.xz).r * w.y + texture(albedo_tex, p.xy).r * w.z;
		if (tex_steps > 0.0) {
			// Normalise the noise ramp (0.7..1) to 0..1, quantise, and map back.
			float n = clamp((t - 0.7) / 0.3, 0.0, 1.0);
			n = posterize(smoothstep(0.25, 0.75, n), tex_steps);
			t = mix(0.74, 1.0, n);
		}
		base *= mix(1.0, t, tex_amount);
	}

	if (shadow_pattern == 2) {
		base *= 1.0 - hatch_darkness * hatch(FRAGCOORD.xy, pattern_scale) * (1.0 - unshaded);
	}
	ALBEDO = base * (1.0 - unshaded);
	// Specular is off, so roughness is free to carry a flag for the ink-line pass: low = glowing.
	ROUGHNESS = (emission_energy + unshaded) > 0.01 ? 0.1 : 1.0;
	METALLIC = 0.0;

	vec3 glow = emission * emission_energy;
	float peak = max(glow.r, max(glow.g, glow.b));
	if (glow_keep_hue > 0.5 && peak > 1.0) {
		float over = peak - 1.0;
		glow = mix(glow / peak, vec3(1.0), clamp(over * 0.2, 0.0, 0.6)) * (1.0 + over * 0.5);
	}
	glow += albedo.rgb * unshaded + base * self_light * (1.0 - unshaded);

	// View-space rim, biased to faces that look up or sideways (sky light), not down.
	float fres = 1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float rim = smoothstep(1.0 - rim_width, 1.0 - rim_width + rim_softness, fres);
	rim *= smoothstep(-0.5, 0.2, world_normal.y) * (1.0 - unshaded);
	glow += rim_color * rim * rim_strength;
	EMISSION = glow;

	if (fog_max > 0.0) {
		float f = clamp((-VERTEX.z - fog_start) / max(fog_end - fog_start, 0.001), 0.0, 1.0);
		if (fog_bands > 0.0) {
			f = ceil(f * fog_bands) / fog_bands;
		}
		float glowing = clamp(emission_energy + unshaded, 0.0, 1.0);
		FOG = vec4(fog_color, f * fog_max * (1.0 - fog_glow_cut * glowing));
	} else {
		FOG = vec4(0.0);
	}
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float x = ndl;
	float falloff = 1.0;
	if (LIGHT_IS_DIRECTIONAL) {
		// Cast shadows drop straight into the shadow band, with a hard edge.
		x = mix(-1.0, ndl, smoothstep(0.35, 0.65, ATTENUATION));
	} else {
		falloff = ATTENUATION;
	}

	if (shadow_pattern > 0) {
		x += (dots(FRAGCOORD.xy, pattern_scale) - 0.5) * pattern_strength;
	}

	float s = band_softness;
	float lit = smoothstep(lit_threshold - s, lit_threshold + s, x);
	float level = lit;
	if (bands >= 3) {
		float mid = smoothstep(mid_threshold - s, mid_threshold + s, x);
		level = mix(mid * mid_level, 1.0, lit);
	}
	vec3 lc = LIGHT_COLOR * falloff / PI;
	if (shadow_pattern == 2 && LIGHT_IS_DIRECTIONAL) {
		// fragment() darkened the albedo along the hatch strokes. Undo that where the
		// surface is lit, so the strokes only show inside the shadow.
		float k = 1.0 - hatch_darkness * hatch(FRAGCOORD.xy, pattern_scale);
		DIFFUSE_LIGHT += level * ((ambient_light + lc) / k - ambient_light);
	} else {
		DIFFUSE_LIGHT += lc * level;
	}
}
