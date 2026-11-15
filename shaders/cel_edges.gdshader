// Screen-space ink lines: a full-screen quad that compares depth and normals around each
// pixel and draws a line where they jump. Only outputs colour + alpha, so it never
// overwrites what is underneath (sparks, flashes).
shader_type spatial;
render_mode unshaded, blend_mix, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D normal_tex : hint_normal_roughness_texture, filter_nearest, repeat_disable;

uniform vec4 ink_color : source_color = vec4(0.05, 0.03, 0.02, 1.0);
/// Line thickness in pixels at a 900 px tall viewport.
uniform float thickness = 1.5;
/// Relative depth jump that counts as an edge.
uniform float depth_threshold = 0.06;
uniform float normal_threshold = 0.35;
/// Inner (normal) lines fade out between these distances; silhouettes stay.
uniform float normal_fade_start = 12.0;
uniform float normal_fade_end = 30.0;
/// Silhouette lines shrink to this fraction of the thickness between these distances.
uniform float far_thickness = 0.55;
uniform float thin_start = 8.0;
uniform float thin_end = 26.0;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float lin_depth(vec2 uv, mat4 inv_proj) {
	float d = texture(depth_tex, uv).r;
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, d, 1.0);
	return min(-v.z / v.w, 300.0);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	float zc = lin_depth(uv, INV_PROJECTION_MATRIX);
	vec4 centre = texture(normal_tex, uv);
	vec3 nc = centre.xyz * 2.0 - 1.0;
	// The toon shader writes a low roughness on glowing parts (heads, eyes, mutation patches),
	// which marks them here: they get no inner lines, so the glow is never inked over.
	bool centre_glows = centre.a < 0.5;
	bool sky = zc >= 299.0;

	// Lines thin out with distance so small far targets are not swallowed.
	float thin = mix(1.0, far_thickness, smoothstep(thin_start, thin_end, zc));
	vec2 px = thickness * thin * (VIEWPORT_SIZE.y / 900.0) / VIEWPORT_SIZE;
	vec2 o[4] = { vec2(px.x, 0.0), vec2(-px.x, 0.0), vec2(0.0, px.y), vec2(0.0, -px.y) };

	float dz = 0.0;
	float dn = 0.0;
	float zmin = zc;
	for (int i = 0; i < 4; i++) {
		float z = lin_depth(uv + o[i], INV_PROJECTION_MATRIX);
		vec4 n = texture(normal_tex, uv + o[i]);
		// Silhouettes: ink the pixel just outside the nearer object, never the object itself.
		dz = max(dz, (zc - z) / z);
		zmin = min(zmin, z);
		if (n.a >= 0.5) {
			dn = max(dn, 1.0 - dot(nc, n.xyz * 2.0 - 1.0));
		}
	}
	// Grazing surfaces (the floor towards the horizon) step in depth every pixel; scale that out.
	vec3 view_dir = normalize((INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, 1.0, 1.0)).xyz);
	float graze = sky ? 1.0 : clamp(abs(dot(nc, -view_dir)), 0.05, 1.0);
	float depth_edge = smoothstep(depth_threshold, depth_threshold * 1.6, dz * graze);
	float normal_edge = smoothstep(normal_threshold, normal_threshold * 1.5, dn);
	normal_edge *= 1.0 - smoothstep(normal_fade_start, normal_fade_end, zmin);
	if (sky || centre_glows) {
		normal_edge = 0.0;
	}

	ALBEDO = ink_color.rgb;
	ALPHA = max(depth_edge, normal_edge) * ink_color.a;
}
