@tool
extends EditorScript

# Builds a fixed 3x2 layout in code and exports it to res://Assets/Scenes/Dungeons/Sample/. Kept as a regression
# check for the builders: run it from the Script editor (File > Run) and play the result with the 4 key.
#
#   Sample A  -  Sample B  =L= Sample C          (=L= locked, |S| shutter)
#      |            |            |S block
#   Sample D  =L=  Start  -S|  Sample F
#                    ^ entrance
#   Basement (custom, off grid), reached by stairs in A that appear when A's block is pushed.

const EXPORT_DIR := "res://Assets/Scenes/Dungeons/Sample"
const ENEMIES := "res://Assets/Scenes/Enemies/"
const COLLECTIBLES := "res://Assets/Scenes/Collectibles/"
const Appears := DMPlacement.Appears


func _run() -> void:
	var result := make()
	print("Sample dungeon: ", "exported" if result.ok else "failed", " ", result)


static func make() -> Dictionary:
	var layout := build_layout()
	return DMExporter.export_dungeon(layout)


static func build_layout() -> DMDungeonLayout:
	var layout := DMDungeonLayout.new()
	layout.dungeon_name = "Sample"
	layout.export_dir = EXPORT_DIR
	layout.theme = load(DMNames.EAGLE_THEME)
	layout.music = load("res://Assets/Sounds/03_-_Legend_of_Zelda_-_NES_-_Underworld_BGM.ogg")
	var stamps := DMThemeStamps.new(layout.theme)
	var brushes := Array(stamps.brush_names())
	stamps.free_stamps()

	var start := _room(layout, "Start", Vector2i(1, 1))
	var a := _room(layout, "SampleA", Vector2i(0, 0))
	var b := _room(layout, "SampleB", Vector2i(1, 0))
	var c := _room(layout, "SampleC", Vector2i(2, 0))
	var d := _room(layout, "SampleD", Vector2i(0, 1))
	var f := _room(layout, "SampleF", Vector2i(2, 1))
	var basement := _room(layout, "SampleBasement", Vector2i.ZERO)
	basement.off_grid = true
	basement.custom_scene = load("res://Assets/Scenes/Rooms/room_basement.tscn")

	layout.start_room_id = start.id
	layout.entrance_side = DMDungeonLayout.DoorSide.DOWN
	layout.entrance_point = Vector2i(128, 136)

	# Start: statues, and a key at a pixel that's on no grid.
	_fill(start, brushes.find("Statue"), Rect2i(2, 1, 1, 1))
	_fill(start, brushes.find("Statue"), Rect2i(9, 1, 1, 1))
	_place(layout, start, COLLECTIBLES + "key.tscn", Vector2i(101, 77))

	# D: water and blocks, Stalfos, and a key that appears where it was placed once the room is cleared.
	_fill(d, brushes.find("Water"), Rect2i(3, 2, 6, 3))
	_fill(d, brushes.find("Block"), Rect2i(1, 1, 1, 1))
	_fill(d, brushes.find("Block"), Rect2i(10, 5, 1, 1))
	_place(layout, d, ENEMIES + "Stalfos.tscn", Vector2i(56, 64))
	_place(layout, d, ENEMIES + "Stalfos.tscn", Vector2i(184, 120))
	_place(layout, d, COLLECTIBLES + "key.tscn", Vector2i(72, 120), Appears.ON_ROOM_CLEARED)

	# B: both dark floors. A Keese flies over the blocking kind, a Stalfos walks around it.
	_fill(b, brushes.find("DarkFloor"), Rect2i(0, 0, 12, 2))
	_fill(b, brushes.find("DarkFloorBlocking"), Rect2i(4, 3, 4, 2))
	_place(layout, b, ENEMIES + "keese.tscn", Vector2i(72, 104))
	_place(layout, b, ENEMIES + "Stalfos.tscn", Vector2i(184, 104))

	# A: a block that can only be pushed up reveals stairs to the basement.
	var block := _place(layout, a, "res://Assets/Scenes/World/pushable_block.tscn", Vector2i(120, 88))
	block.overrides["AllowedDirection"] = Vector2.UP
	var stairs := DMWarpLink.new()
	stairs.from_room_id = a.id
	stairs.from_position = Vector2i(152, 88)
	stairs.warp_scene = load(DMNames.STAIRS_SCENE)
	stairs.to_room_id = basement.id
	stairs.arrival_position = Vector2i(56, 24)
	stairs.appears = Appears.ON_BLOCK_PUSHED
	stairs.block_id = block.id
	stairs.two_way = true
	stairs.return_position = Vector2i(56, 4)
	stairs.return_arrival_position = Vector2i(136, 88)
	layout.warps.append(stairs)

	# F: a shutter back to Start that closes until the room is cleared, and a shutter up to C that opens when
	# the block is pushed. Each only opens on its own event.
	var f_block := _place(layout, f, "res://Assets/Scenes/World/pushable_block.tscn", Vector2i(184, 72))
	_place(layout, f, ENEMIES + "gel.tscn", Vector2i(88, 104))
	_place(layout, f, ENEMIES + "gel.tscn", Vector2i(120, 72))
	_place(layout, c, ENEMIES + "goriya.tscn", Vector2i(136, 88))
	_place(layout, c, COLLECTIBLES + "heart_container.tscn", Vector2i(128, 56), Appears.ON_ROOM_CLEARED)

	_edge(layout, start, b)
	_edge(layout, start, d).kind = DMDoorEdge.Kind.LOCKED
	var start_f := _edge(layout, start, f)
	start_f.shutter_for(f.id).mode = DMShutterRule.Mode.UNTIL_CLEARED
	_edge(layout, a, b)
	_edge(layout, a, d)
	_edge(layout, b, c).kind = DMDoorEdge.Kind.LOCKED
	var c_f := _edge(layout, c, f)
	c_f.shutter_for(f.id).mode = DMShutterRule.Mode.UNTIL_BLOCK_PUSHED
	c_f.shutter_for(f.id).block_id = f_block.id
	return layout


static func _room(layout: DMDungeonLayout, room_name: String, grid_pos: Vector2i) -> DMRoomLayout:
	var room := layout.new_room(grid_pos)
	room.room_name = room_name
	layout.rooms.append(room)
	return room


static func _edge(layout: DMDungeonLayout, one: DMRoomLayout, two: DMRoomLayout) -> DMDoorEdge:
	var edge := layout.make_edge(one, two)
	layout.edges.append(edge)
	return edge


static func _fill(room: DMRoomLayout, brush: int, rect: Rect2i) -> void:
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			room.tiles[y * DMRoomLayout.TILES_WIDE + x] = brush


static func _place(layout: DMDungeonLayout, room: DMRoomLayout, scene_path: String, position: Vector2i, appears := Appears.IMMEDIATELY) -> DMPlacement:
	var placement := DMPlacement.new()
	placement.id = layout.new_placement_id()
	placement.scene = load(scene_path)
	placement.position = position
	placement.appears = appears
	room.placements.append(placement)
	return placement
