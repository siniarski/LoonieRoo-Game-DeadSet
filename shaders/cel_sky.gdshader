// Stylised dusk sky: a three-stop gradient (optionally cut into flat bands), a graphic sun
// disc where the scene's directional light is, far ridgelines and optional stars.
shader_type sky;

uniform vec3 top_color : source_color = vec3(0.42, 0.3, 0.26);
uniform vec3 mid_color : source_color = vec3(0.7, 0.45, 0.32);
uniform vec3 horizon_color : source_color = vec3(0.86, 0.58, 0.38);
uniform vec3 ground_color : source_color = vec3(0.25, 0.17, 0.12);
/// Elevation (0..1 of straight up) where the gradient reaches the top colour.
uniform float gradient_height = 0.6;
uniform float mid_point = 0.35;
/// 0 = smooth, otherwise the number of flat bands.
uniform float bands = 0.0;
/// Keeps the bands level on screen, like printed stripes, instead of curving with the lens.
/// StyleApplier feeds view_forward (the camera's flattened forward) every frame.
uniform bool flat_bands = false;
uniform vec3 view_forward = vec3(0.0, 0.0, -1.0);
/// Halftone dots on the band edges (comic print), in pixels. 0 = off.
uniform float band_dots = 0.0;

uniform vec3 sun_color : source_color = vec3(1.0, 0.8, 0.5);
uniform float sun_size = 0.08;
uniform float sun_energy = 1.0;
/// Flat concentric rings around the sun (poster look). 0 = none.
uniform float sun_rings = 0.0;
uniform float sun_ring_step = 0.07;
uniform float sun_halo = 0.0;

uniform vec3 ridge_color : source_color = vec3(0.3, 0.16, 0.14);
uniform vec3 ridge_far_color : source_color = vec3(0.5, 0.3, 0.26);
/// Elevation of the near ridgeline (0 = none).
uniform float ridge_height = 0.0;

uniform float stars = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float ridge(float az, float seed) {
	// Flat-topped mesas: a few sines, clipped.
	float v = sin(az * 3.0 + seed) * 0.5 + sin(az * 7.0 + seed * 2.3) * 0.3 + sin(az * 17.0 + seed * 4.1) * 0.2;
	// Clip the peaks flat (mesas) and keep the valleys soft.
	v = smoothstep(-0.7, 0.45, v);
	return v;
}

void sky() {
	vec3 dir = normalize(EYEDIR);
	float h = dir.y;
	float t = clamp(h / gradient_height, 0.0, 1.0);
	if (flat_bands && !AT_CUBEMAP_PASS) {
		t = clamp(h / max(dot(dir.xz, view_forward.xz), 0.001) / gradient_height, 0.0, 1.0);
	}
	if (bands > 0.0) {
		float scaled = t * bands;
		float cell = floor(scaled);
		if (band_dots > 0.0 && !AT_CUBEMAP_PASS) {
			// Dots thin out towards the top of each band, like a printed gradient.
			mat2 rot = mat2(vec2(0.7071, -0.7071), vec2(0.7071, 0.7071));
			vec2 c = fract(rot * FRAGCOORD.xy / band_dots) - 0.5;
			float f = fract(scaled);
			if (length(c) * 1.5 < (f - 0.45) * 1.6) {
				cell += 1.0;
			}
		}
		t = clamp(cell / max(bands - 1.0, 1.0), 0.0, 1.0);
	}
	vec3 col = t < mid_point
		? mix(horizon_color, mid_color, t / max(mid_point, 0.001))
		: mix(mid_color, top_color, (t - mid_point) / max(1.0 - mid_point, 0.001));

	if (stars > 0.0 && h > 0.0) {
		vec2 sp = dir.xz / (abs(dir.y) + 0.35) * 60.0;
		vec2 id = floor(sp);
		float r = hash(id);
		vec2 c = fract(sp) - 0.5 - (vec2(hash(id + 3.1), hash(id + 7.7)) - 0.5) * 0.6;
		float star = step(0.93, r) * (1.0 - smoothstep(0.03, 0.09, length(c)));
		col += vec3(1.0, 0.95, 0.85) * star * stars * smoothstep(0.15, 0.6, t);
	}

	float ang = LIGHT0_ENABLED ? acos(clamp(dot(dir, LIGHT0_DIRECTION), -1.0, 1.0)) : 10.0;
	if (sun_rings > 0.0) {
		float ring = floor((ang - sun_size) / sun_ring_step);
		if (ang > sun_size && ring < sun_rings) {
			col = mix(col, sun_color, 0.45 * (1.0 - (ring + 1.0) / (sun_rings + 1.0)));
		}
	}
	col += sun_color * sun_halo * exp(-ang * 5.0);
	float disc = 1.0 - smoothstep(sun_size, sun_size + 0.004, ang);
	col = mix(col, sun_color * sun_energy, disc);

	if (ridge_height > 0.0) {
		float az = atan(dir.x, dir.z);
		if (h < ridge_height * (0.55 + 0.65 * ridge(az, 1.7))) {
			col = ridge_far_color;
		}
		if (h < ridge_height * (0.25 + 0.5 * ridge(az * 1.3, 5.2))) {
			col = ridge_color;
		}
	}
	if (h < 0.0) {
		col = ground_color;
	}
	COLOR = col;
}
