@tool
class_name DMThemeStamps
extends RefCounted

# Reads stamps out of a theme's stamp scene and pastes them into tile layers.

var theme: DMDungeonTheme
var root: Node
var brushes: Array[Node] = []


func _init(dungeon_theme: DMDungeonTheme) -> void:
	theme = dungeon_theme
	root = theme.stamps.instantiate()
	var brush_parent := root.get_node_or_null("Brushes")
	if brush_parent:
		for child in brush_parent.get_children():
			brushes.append(child)


func free_stamps() -> void:
	if is_instance_valid(root):
		root.free()


func stamp(path: String) -> Node:
	return root.get_node_or_null(path)


func brush(index: int) -> Node:
	if brushes.is_empty():
		return null
	return brushes[index] if index >= 0 and index < brushes.size() else brushes[0]


func brush_names() -> PackedStringArray:
	var names := PackedStringArray()
	for b in brushes:
		names.append(b.name)
	return names


# A brush blocks if any of its cells are on a colliding layer.
func brush_blocks(index: int) -> bool:
	var b := brush(index)
	for layer_name in ["WallLayer", "LowWallLayer"]:
		var layer: TileMapLayer = b.get_node_or_null(layer_name) if b else null
		if layer and not layer.get_used_cells().is_empty():
			return true
	return false


# A representative colour per brush, for map thumbnails of rooms that haven't been previewed yet.
func brush_color(index: int) -> Color:
	var b := brush(index)
	if b == null:
		return Color.BLACK
	for layer_name in ["LowWallLayer", "WallLayer", "GroundLayer"]:
		var layer: TileMapLayer = b.get_node_or_null(layer_name)
		if layer == null or layer.get_used_cells().is_empty():
			continue
		var cell: Vector2i = layer.get_used_cells()[0]
		var source := theme.wall_tile_set.get_source(layer.get_cell_source_id(cell)) as TileSetAtlasSource
		var region := source.get_tile_texture_region(map_coords(layer.get_cell_atlas_coords(cell)))
		var image := source.texture.get_image()
		if image.is_compressed():
			image.decompress()
		return image.get_region(region).get_pixel(region.size.x / 2, region.size.y / 2)
	return Color.BLACK


# The cells a stamp covers on any layer.
func used_rect(stamp_node: Node) -> Rect2i:
	var rect := Rect2i()
	var first := true
	for layer_name in DMNames.LAYERS:
		var layer: TileMapLayer = stamp_node.get_node_or_null(layer_name)
		if layer == null or layer.get_used_cells().is_empty():
			continue
		var r := layer.get_used_rect()
		rect = r if first else rect.merge(r)
		first = false
	return rect


func map_coords(coords: Vector2i) -> Vector2i:
	if theme.palette_column_offset != 0 and coords.x >= 0 and coords.x < 64:
		coords.x = posmod(coords.x + theme.palette_column_offset, 64)
	return coords


func paste(stamp_node: Node, layers: Dictionary, offset := Vector2i.ZERO) -> void:
	for layer_name in DMNames.LAYERS:
		var from: TileMapLayer = stamp_node.get_node_or_null(layer_name)
		if from == null:
			continue
		var to: TileMapLayer = layers[layer_name]
		for cell in from.get_used_cells():
			to.set_cell(cell + offset, from.get_cell_source_id(cell), map_coords(from.get_cell_atlas_coords(cell)), from.get_cell_alternative_tile(cell))


func copy_cell(from: TileMapLayer, from_cell: Vector2i, to: TileMapLayer, to_cell: Vector2i) -> void:
	if from == null or from.get_cell_source_id(from_cell) == -1:
		to.erase_cell(to_cell)
		return
	to.set_cell(to_cell, from.get_cell_source_id(from_cell), map_coords(from.get_cell_atlas_coords(from_cell)), from.get_cell_alternative_tile(from_cell))


static func clear_rect(layers: Dictionary, layer_names: Array, rect: Rect2i) -> void:
	for layer_name in layer_names:
		var layer: TileMapLayer = layers[layer_name]
		for y in range(rect.position.y, rect.end.y):
			for x in range(rect.position.x, rect.end.x):
				layer.erase_cell(Vector2i(x, y))
