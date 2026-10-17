shader_type canvas_item;

// Paints every visible pixel one flat colour, keeping only the texture's shape. Modulate is ignored on
// purpose, so an outline stays the same colour however its sprite is tinted.
uniform vec4 color : source_color = vec4(1.0);

void fragment() {
	COLOR = vec4(color.rgb, texture(TEXTURE, UV).a * color.a);
}
