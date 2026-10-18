@tool
extends EditorScript

# One-off: builds the Eagle theme's stamp scene from Dungeon 1's rooms. Run it from the Script editor
# (File > Run). Keep it, so the theme can be rebuilt if the rooms change.

const ROOMS := "res://Assets/Scenes/Rooms/"
const THEME_DIR := "res://addons/dungeon_maker/themes/eagle/"
const WALL_TILE_SET := "res://Assets/Resources/dungeon_tile_set.tres"
const LOW_WALL_TILE_SET := "res://Assets/Resources/dungeon_low_wall_tile_set.tres"
const SOURCE_ID := 1

# Door regions in cells, from the decoded rooms.
const DOORWAYS := {
	"Up": Rect2i(14, 0, 4, 5),
	"Down": Rect2i(14, 18, 4, 4),
	"Left": Rect2i(0, 9, 4, 4),
	"Right": Rect2i(28, 9, 4, 4),
}
const STRIP_TILES := [Vector2i(42, 6), Vector2i(43, 6)]
const FLOOR := [[Vector2i(42, 6), Vector2i(43, 6)], [Vector2i(42, 7), Vector2i(43, 7)]]
# Most Dungeon 1 rooms use the teal block. Room 1's blue statues are kept as their own brush.
const BLOCK := [[Vector2i(40, 10), Vector2i(41, 10)], [Vector2i(40, 11), Vector2i(41, 11)]]
const STATUE := [[Vector2i(58, 8), Vector2i(59, 8)], [Vector2i(58, 9), Vector2i(59, 9)]]
const WATER := [[Vector2i(3, 3), Vector2i(3, 3)], [Vector2i(3, 3), Vector2i(3, 3)]]
const DOOR_SCENES := {
	"Locked": "res://Assets/Scenes/Locked Doors/Door Locked %s.tscn",
	"Shutter": "res://Assets/Scenes/Shutter Doors/Shutter %s.tscn",
}


func _run() -> void:
	extract()


static func extract() -> Error:
	var wall_set: TileSet = load(WALL_TILE_SET)
	var stamps := Node2D.new()
	stamps.name = "EagleStamps"

	var room2: Node = load(ROOMS + "room2.tscn").instantiate()
	var room3: Node = load(ROOMS + "room3.tscn").instantiate()
	var room4: Node = load(ROOMS + "room4.tscn").instantiate()
	var room5: Node = load(ROOMS + "room5.tscn").instantiate()
	var room1: Node = load(ROOMS + "room1.tscn").instantiate()

	# Shell: room2 is closed on three sides. Its open right side comes from room3, which is closed there.
	var shell := _group(stamps, "Shell")
	var shell_layers := _add_layers(shell, wall_set)
	for layer_name in ["GroundLayer", "WallLayer", "OverheadLayer"]:
		_copy_rect(room2.get_node(layer_name), shell_layers[layer_name], Rect2i(0, 0, 32, 22))
	_clear(shell_layers, DOORWAYS["Right"])
	for layer_name in ["GroundLayer", "WallLayer", "OverheadLayer"]:
		_copy_rect(room3.get_node(layer_name), shell_layers[layer_name], DOORWAYS["Right"])
	# The LowWall copy of the outer wall in some rooms is left out, and the strip uses one tile.
	for x in range(4, 28):
		shell_layers["LowWallLayer"].set_cell(Vector2i(x, 4), SOURCE_ID, STRIP_TILES[x % 2])
	for y in range(4, 18):
		for x in range(4, 28):
			shell_layers["GroundLayer"].set_cell(Vector2i(x, y), SOURCE_ID, FLOOR[y % 2][x % 2])

	# Doorways: room5 has all four doors open, but its top one is a bombed hole, so the regular top doorway comes
	# from room4. Strip cells in a doorway use the same strip tile.
	var doorways := _group(stamps, "Doorways")
	for side in DOORWAYS:
		var source: Node = room4 if side == "Up" else room5
		var layers := _add_layers(_group(doorways, side), wall_set)
		for layer_name in DMNames.LAYERS:
			_copy_rect(source.get_node(layer_name), layers[layer_name], DOORWAYS[side])
		_normalise_strip(layers["LowWallLayer"])
	var entrance := _add_layers(_group(doorways, "DownEntrance"), wall_set)
	for layer_name in DMNames.LAYERS:
		_copy_rect(room1.get_node(layer_name), entrance[layer_name], DOORWAYS["Down"])

	# Brushes, each a 2x2 stamp. The layer a cell is on decides its collision.
	var brushes := _group(stamps, "Brushes")
	var dark := _find_dark_tile(wall_set)
	if dark == Vector2i(-1, -1):
		push_error("No fully black, opaque tile in the theme's columns.")
		return ERR_CANT_CREATE
	var dark_tiles := [[dark, dark], [dark, dark]]
	# Each brush's description is the palette's tooltip.
	_brush(brushes, wall_set, "Floor", FLOOR, [], "Plain floor. Everything can walk on it.")
	_brush(brushes, wall_set, "Block", FLOOR, BLOCK, "A solid block. Stops Lonk and walking enemies. Arrows, thrown weapons and flying enemies (Keese) pass over it.")
	_brush(brushes, wall_set, "Water", FLOOR, WATER, "Water. Stops Lonk and walking enemies. Arrows, thrown weapons and flying enemies pass over it.")
	_brush(brushes, wall_set, "DarkFloor", dark_tiles, [], "Black floor that can be walked on, like an unlit room.")
	_brush(brushes, wall_set, "DarkFloorBlocking", dark_tiles, dark_tiles, "Black floor that can't be walked on (a pit). Blocks like water: Lonk and walking enemies stop, projectiles and flying enemies pass over.")
	_brush(brushes, wall_set, "Statue", FLOOR, STATUE, "A statue, as in the entrance room. Blocks like a block.")

	# Doors, instanced at the positions their scenes expect.
	var doors := _group(stamps, "Doors")
	for kind in DOOR_SCENES:
		var group := _group(doors, kind)
		for side in DOORWAYS:
			var door: Node2D = load(DOOR_SCENES[kind] % side).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
			door.name = side
			group.add_child(door)
	var locked_up: Node2D = doors.get_node("Locked/Up")
	locked_up.position = room1.get_node("DoorLockedUp").position

	for room in [room1, room2, room3, room4, room5]:
		room.free()

	_report_cells_without_polygons(stamps, wall_set)
	_own(stamps, stamps)
	var packed := PackedScene.new()
	var error := packed.pack(stamps)
	stamps.free()
	if error != OK:
		return error
	var stamps_path := THEME_DIR + "eagle_stamps.tscn"
	var keep_uid := ResourceLoader.get_resource_uid(stamps_path) if ResourceLoader.exists(stamps_path) else ResourceUID.INVALID_ID
	error = ResourceSaver.save(packed, stamps_path)
	if error != OK:
		return error
	DMNames.ensure_uid(stamps_path, keep_uid)

	var theme_path := THEME_DIR + "eagle_theme.tres"
	var dungeon_theme: DMDungeonTheme = load(theme_path) if ResourceLoader.exists(theme_path) else DMDungeonTheme.new()
	dungeon_theme.stamps = load(stamps_path)
	dungeon_theme.wall_tile_set = wall_set
	dungeon_theme.low_wall_tile_set = load(LOW_WALL_TILE_SET)
	dungeon_theme.entity_folders = PackedStringArray([
		"res://Assets/Scenes/Enemies/",
		"res://Assets/Scenes/Collectibles/",
		"res://Assets/Scenes/World/",
	])
	keep_uid = ResourceLoader.get_resource_uid(theme_path) if ResourceLoader.exists(theme_path) else ResourceUID.INVALID_ID
	error = ResourceSaver.save(dungeon_theme, theme_path)
	if error == OK:
		DMNames.ensure_uid(theme_path, keep_uid)
		print("Eagle theme written to ", THEME_DIR)
	return error


static func _group(parent: Node, group_name: String) -> Node2D:
	var node := Node2D.new()
	node.name = group_name
	parent.add_child(node)
	return node


static func _add_layers(parent: Node, tile_set: TileSet) -> Dictionary:
	var layers := {}
	for layer_name in DMNames.LAYERS:
		var layer := TileMapLayer.new()
		layer.name = layer_name
		layer.tile_set = tile_set
		layer.collision_enabled = false
		parent.add_child(layer)
		layers[layer_name] = layer
	return layers


static func _copy_rect(from: TileMapLayer, to: TileMapLayer, rect: Rect2i) -> void:
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			var cell := Vector2i(x, y)
			if from.get_cell_source_id(cell) == -1:
				to.erase_cell(cell)
			else:
				to.set_cell(cell, from.get_cell_source_id(cell), from.get_cell_atlas_coords(cell), from.get_cell_alternative_tile(cell))


static func _clear(layers: Dictionary, rect: Rect2i) -> void:
	DMThemeStamps.clear_rect(layers, DMNames.LAYERS, rect)


static func _normalise_strip(low_wall: TileMapLayer) -> void:
	for cell in low_wall.get_used_cells():
		if cell.y == 4:
			low_wall.set_cell(cell, SOURCE_ID, STRIP_TILES[cell.x % 2])


static func _brush(parent: Node, tile_set: TileSet, brush_name: String, ground: Array, low_wall: Array, description: String) -> void:
	var group := _group(parent, brush_name)
	group.editor_description = description
	var layers := _add_layers(group, tile_set)
	for y in 2:
		for x in 2:
			layers["GroundLayer"].set_cell(Vector2i(x, y), SOURCE_ID, ground[y][x])
			if not low_wall.is_empty():
				layers["LowWallLayer"].set_cell(Vector2i(x, y), SOURCE_ID, low_wall[y][x])
	# Empty layers are dropped so a brush only carries what it paints.
	for layer_name in layers:
		if layers[layer_name].get_used_cells().is_empty():
			layers[layer_name].free()


# A transparent tile would show the viewport's clear colour, so the dark floor must be truly black. The black
# band in rows 4-5 is transparent, so this looks through the theme's columns (32-47) for an opaque one.
static func _find_dark_tile(tile_set: TileSet) -> Vector2i:
	var source: TileSetAtlasSource = tile_set.get_source(SOURCE_ID)
	var image := source.texture.get_image()
	if image.is_compressed():
		image.decompress()
	for y in source.get_atlas_grid_size().y:
		for x in range(32, 48):
			var coords := Vector2i(x, y)
			if not source.has_tile(coords):
				continue
			var region := image.get_region(source.get_tile_texture_region(coords))
			var black := true
			for py in region.get_height():
				for px in region.get_width():
					var c := region.get_pixel(px, py)
					if c.a < 1.0 or c.r > 0.01 or c.g > 0.01 or c.b > 0.01:
						black = false
			if black:
				return coords
	return Vector2i(-1, -1)


# Cells on colliding layers whose tile has no polygon would not collide. Every tile in the shared TileSets has
# a full polygon, so this should print nothing.
static func _report_cells_without_polygons(stamps: Node, tile_set: TileSet) -> void:
	var source: TileSetAtlasSource = tile_set.get_source(SOURCE_ID)
	var stack: Array[Node] = [stamps]
	while not stack.is_empty():
		var node: Node = stack.pop_back()
		stack.append_array(node.get_children())
		if node is TileMapLayer and node.name in ["WallLayer", "LowWallLayer"]:
			for cell in node.get_used_cells():
				var coords: Vector2i = node.get_cell_atlas_coords(cell)
				if source.get_tile_data(coords, 0).get_collision_polygons_count(0) == 0:
					print("No polygon: %s at %s in %s" % [coords, cell, stamps.get_path_to(node)])


static func _own(node: Node, owner_node: Node) -> void:
	for child in node.get_children():
		child.owner = owner_node
		# Children of an instanced door belong to the door's own scene.
		if child.scene_file_path.is_empty():
			_own(child, owner_node)
