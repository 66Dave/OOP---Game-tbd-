shader_type spatial;
render_mode cull_disabled;   // grass blades / leaf cards are flat planes, show both sides

// --- Look ---
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap;
uniform bool use_texture = true;            // turn off if the model has no texture
uniform vec4 tint : source_color = vec4(1.0);
uniform float alpha_cutoff = 0.5;           // cuts out transparent parts of the texture

// --- Wind ---
uniform float sway_strength = 0.1;          // how far it bends
uniform float sway_speed = 2.0;             // how fast it waves
uniform float sway_scale = 0.7;             // neighbors out of sync
uniform float bend_power = 1.0;             // 1 = grass (bends evenly), 2 = trees (stiff trunk, loose top)
uniform float flutter = 0.0;                // small fast shake for leaves (0 = off, try 0.02)

void vertex() {
	// Bottom (y = 0) stays planted, higher parts bend more
	float h = pow(max(VERTEX.y, 0.0), bend_power);

	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 origin = MODEL_MATRIX[3].xyz;   // whole object sways together, neighbors differ
	float wave = sin(TIME * sway_speed + origin.x * sway_scale + origin.z * sway_scale * 0.7);
	float gust = sin(TIME * sway_speed * 0.37 + origin.z * 0.2) * 0.5 + 0.5;

	VERTEX.x += wave * sway_strength * h * (0.6 + gust * 0.4);
	VERTEX.z += wave * sway_strength * 0.5 * h;

	// leaf flutter: tiny fast wiggle that varies per vertex
	VERTEX += NORMAL * sin(TIME * 9.0 + world.x * 3.1 + world.y * 4.7 + world.z * 2.3) * flutter;
}

void fragment() {
	vec4 c = tint;
	if (use_texture) {
		c *= texture(albedo_texture, UV);
	}
	ALBEDO = c.rgb;
	ALPHA = c.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_cutoff;
}
