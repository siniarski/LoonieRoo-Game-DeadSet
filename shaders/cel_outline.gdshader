// Inverted-hull outline, added as the toon material's next_pass.
// The shell is pushed out in clip space so the line keeps a steady pixel width.
shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;

uniform vec4 outline_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
/// Line width in pixels at a 900 px tall viewport, up close.
uniform float width = 3.0;
/// Beyond this distance the line thins out, down to min_width.
uniform float thin_distance = 10.0;
uniform float min_width = 1.2;
/// 0 = push along the normal (spheres, capsules); 1 = box corners; 2 = cylinder rims.
/// Hard-edged primitives split their normals at the edges, so they need their own direction.
uniform int shape = 0;
uniform float glow = 0.0;

void vertex() {
	vec3 dir = NORMAL;
	if (shape == 1) {
		dir = sign(VERTEX);
	} else if (shape == 2) {
		vec2 r = length(VERTEX.xz) > 0.0001 ? normalize(VERTEX.xz) : vec2(0.0);
		dir = vec3(r.x, sign(VERTEX.y), r.y);
	}
	vec4 clip = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
	vec3 view_dir = normalize((MODELVIEW_MATRIX * vec4(dir, 0.0)).xyz);
	vec4 clip_dir = PROJECTION_MATRIX * vec4(view_dir, 0.0);
	vec2 d = clip_dir.xy;
	float len = length(d * VIEWPORT_SIZE);
	if (len > 0.0001) {
		d = d * VIEWPORT_SIZE / len;   // unit vector in pixel space
		float px = width * VIEWPORT_SIZE.y / 900.0;
		float thin = clamp(thin_distance / max(clip.w, 0.001), 0.0, 1.0);
		px = max(px * thin, min(min_width, px));
		clip.xy += d * px * 2.0 / VIEWPORT_SIZE * clip.w;
	}
	// Sit a hair behind the surface, so shells of touching boxes do not z-fight with their faces.
	clip.z -= 0.00002 * clip.w;
	POSITION = clip;
}

void fragment() {
	ALBEDO = outline_color.rgb * (1.0 + glow);
}
