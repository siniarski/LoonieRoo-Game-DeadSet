// Final colour grade, drawn on a CanvasLayer below the HUD so the HUD keeps its own colours.
shader_type canvas_item;
render_mode unshaded, blend_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_nearest, repeat_disable;

uniform float saturation = 1.0;
uniform float contrast = 1.0;
uniform float brightness = 1.0;
/// Split tone: shadows drift to one colour, highlights to another.
uniform vec3 shadow_tint : source_color = vec3(0.5);
uniform vec3 highlight_tint : source_color = vec3(0.5);
uniform float tint_amount = 0.0;
/// Multiplied over everything, like printing on tinted paper.
uniform vec3 paper_color : source_color = vec3(1.0);
/// Raises the darkest value (faded print).
uniform float black_lift = 0.0;
/// Quantises brightness into flat steps, keeping hue (0 = off).
uniform float posterize_steps = 0.0;
uniform float grain = 0.0;
uniform float vignette = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec3 c = texture(screen_tex, SCREEN_UV).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(l), c, saturation);
	c = (c - 0.5) * contrast + 0.5;
	c *= brightness;
	c = clamp(c, 0.0, 1.0);

	l = dot(c, vec3(0.299, 0.587, 0.114));
	if (posterize_steps > 0.0) {
		float q = floor(l * posterize_steps + 0.5) / posterize_steps;
		c *= q / max(l, 0.001);
		c = clamp(c, 0.0, 1.0);
		l = q;
	}
	// Soft-light style split tone.
	vec3 tint = mix(shadow_tint, highlight_tint, smoothstep(0.15, 0.85, l));
	c = mix(c, c + (tint - 0.5) * 2.0 * (1.0 - abs(2.0 * c - 1.0) * 0.5) * 0.5, tint_amount);

	c = mix(vec3(black_lift), vec3(1.0), c);
	c *= paper_color;

	if (grain > 0.0) {
		// Static paper tooth: two scales of noise locked to the screen.
		vec2 p = floor(FRAGCOORD.xy / 2.0);
		float n = hash(p) * 0.6 + hash(floor(FRAGCOORD.xy / 7.0)) * 0.4;
		c *= 1.0 - grain * n;
	}
	if (vignette > 0.0) {
		vec2 v = SCREEN_UV - 0.5;
		c *= 1.0 - vignette * smoothstep(0.25, 0.85, length(v) * 1.2);
	}
	COLOR = vec4(clamp(c, 0.0, 1.0), 1.0);
}
