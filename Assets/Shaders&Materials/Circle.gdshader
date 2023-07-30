shader_type canvas_item;

uniform vec4 color : source_color;
uniform float thickness : hint_range(0.0, 1.0, 0.01) = 1.0;
uniform float radius : hint_range(0.0, 1.0, 0.01) = 1.0; // radius normalized

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	const float fade = 0.05;
	
	float d = length(uv);
	float circle_alpha = (1.0 - smoothstep(radius - fade, radius, d));
	circle_alpha *= smoothstep(radius - thickness - fade, radius - thickness, d);
	
	COLOR = color;
	COLOR.a *= circle_alpha;
}
