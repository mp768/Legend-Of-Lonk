@tool
class_name DMRoomBuilder
extends RefCounted

# Turns a DMRoomLayout into a Room node tree. The editor preview and the exporter both use it, so the preview
# always matches the game.

const DoorSide := DMDungeonLayout.DoorSide
const Appears := DMPlacement.Appears
const SIDES := [DoorSide.UP, DoorSide.DOWN, DoorSide.LEFT, DoorSide.RIGHT]

var layout: DMDungeonLayout
var stamps: DMThemeStamps
# Hidden objects are drawn translucent instead of being left visible.
var preview := false

var _exit_shape: RectangleShape2D


func _init(dungeon_layout: DMDungeonLayout, theme_stamps: DMThemeStamps, for_preview := false) -> void:
	layout = dungeon_layout
	stamps = theme_stamps
	preview = for_preview
	_exit_shape = RectangleShape2D.new()
	_exit_shape.size = Vector2(DMNames.TILE_SIZE, DMNames.TILE_SIZE)


func build(room: DMRoomLayout) -> Node2D:
	var root := Node2D.new()
	root.name = DMNames.node_name(room.room_name)
	root.set_script(load(DMNames.ROOM_SCRIPT))
	root.set(DMNames.ROOM_VIEW, room.view)

	var layers := _make_layers(root)
	stamps.paste(stamps.stamp("Shell"), layers)
	var doorways := _doorway_stamps(room)
	for side in doorways:
		DMThemeStamps.clear_rect(layers, DMNames.LAYERS, stamps.used_rect(doorways[side]))
		stamps.paste(doorways[side], layers)
	_paint_tiles(room, layers, doorways.get(DoorSide.UP))

	_add_exits(root)
	var shutters := _add_doors(room, root)
	var objects := _add_placements(room, root)
	_wire(room, root, shutters, objects)
	_add_entry_text(room, root)

	_own(root, root)
	return root


func _make_layers(root: Node2D) -> Dictionary:
	var layers := {}
	for layer_name in DMNames.LAYERS:
		var layer := TileMapLayer.new()
		layer.name = layer_name
		var colliding: bool = layer_name == "WallLayer" or layer_name == "LowWallLayer"
		layer.tile_set = stamps.theme.low_wall_tile_set if layer_name == "LowWallLayer" else stamps.theme.wall_tile_set
		layer.collision_enabled = colliding
		if colliding:
			layer.add_to_group(DMNames.WALL_GROUP, true)
		if layer_name == "OverheadLayer":
			layer.z_index = 10
		root.add_child(layer)
		layers[layer_name] = layer
	return layers


# The doorway stamp for every side with a door, or the entrance side of the starting room.
func _doorway_stamps(room: DMRoomLayout) -> Dictionary:
	var doorways := {}
	for side in SIDES:
		var side_name := DMDungeonLayout.side_name(side)
		var stamp: Node = null
		if layout.edge_on(room, side):
			stamp = stamps.stamp("Doorways/" + side_name)
		elif room.id == layout.start_room_id and layout.entrance_side == side:
			stamp = stamps.stamp("Doorways/%sEntrance" % side_name)
			if stamp == null:
				stamp = stamps.stamp("Doorways/" + side_name)
		if stamp:
			doorways[side] = stamp
	return doorways


func _paint_tiles(room: DMRoomLayout, layers: Dictionary, top_doorway: Node) -> void:
	var top_rect := stamps.used_rect(top_doorway) if top_doorway else Rect2i()
	var top_low_wall: TileMapLayer = top_doorway.get_node_or_null("LowWallLayer") if top_doorway else null
	for ty in DMRoomLayout.TILES_HIGH:
		for tx in DMRoomLayout.TILES_WIDE:
			var index := room.get_tile(Vector2i(tx, ty))
			var origin := DMNames.INTERIOR_ORIGIN + Vector2i(tx, ty) * 2
			DMThemeStamps.clear_rect(layers, ["GroundLayer", "WallLayer", "LowWallLayer"], Rect2i(origin, Vector2i(2, 2)))
			stamps.paste(stamps.brush(index), layers, origin)
			if ty == 0 and not stamps.brush_blocks(index):
				_paint_strip(index, origin, layers, top_rect, top_low_wall)


# The strip under the top wall stops bodies 8 px short of it. It goes back over open floor as a copy of that
# floor's own top row on LowWall, so it blends with whatever floor is there. Blocking tiles already collide and
# keep their own top half, and a top doorway keeps its gap.
func _paint_strip(index: int, origin: Vector2i, layers: Dictionary, top_rect: Rect2i, top_low_wall: TileMapLayer) -> void:
	var ground: TileMapLayer = stamps.brush(index).get_node_or_null("GroundLayer")
	for dx in 2:
		var cell := Vector2i(origin.x + dx, DMNames.STRIP_ROW)
		if top_rect.has_point(cell) and (top_low_wall == null or top_low_wall.get_cell_source_id(cell) == -1):
			continue
		stamps.copy_cell(ground, Vector2i(dx, 0), layers["LowWallLayer"], cell)


# All four exits, always. Room ignores an exit whose neighbour is null.
func _add_exits(root: Node2D) -> void:
	for side in DMNames.EXITS:
		var exit := Area2D.new()
		exit.name = DMNames.EXITS[side][0]
		exit.position = DMNames.EXITS[side][1]
		exit.collision_layer = 0
		exit.collision_mask = DMNames.PLAYER_BODY_LAYER
		var shape := CollisionShape2D.new()
		shape.name = "CollisionShape2D"
		shape.shape = _exit_shape
		exit.add_child(shape)
		root.add_child(exit)


# Returns side -> shutter node.
func _add_doors(room: DMRoomLayout, root: Node2D) -> Dictionary:
	var shutters := {}
	var lock_until_cleared := false
	for side in SIDES:
		var edge := layout.edge_on(room, side)
		if edge == null:
			continue
		var side_name := DMDungeonLayout.side_name(side)
		if edge.kind == DMDoorEdge.Kind.LOCKED:
			var door := _instance_door("Doors/Locked/" + side_name)
			door.name = "DoorLocked" + side_name
			root.add_child(door)
		var rule := edge.shutter_for(room.id)
		if rule.mode == DMShutterRule.Mode.NONE:
			continue
		var shutter := _instance_door("Doors/Shutter/" + side_name)
		shutter.name = "Shutter" + side_name
		var cleared := rule.mode == DMShutterRule.Mode.UNTIL_CLEARED
		# A clear-driven shutter starts open and the room closes it on entry while enemies remain.
		shutter.set(DMNames.DOOR_STARTS_OPEN, cleared)
		shutter.set(DMNames.SHUTTER_OPENS_WHEN_CLEARED, cleared)
		lock_until_cleared = lock_until_cleared or cleared
		root.add_child(shutter)
		shutters[side] = shutter
	root.set(DMNames.ROOM_LOCK_UNTIL_CLEARED, lock_until_cleared)
	return shutters


# Door scenes are positioned by the stamp scene itself.
func _instance_door(stamp_path: String) -> Node2D:
	var stamp: Node2D = stamps.stamp(stamp_path)
	var door: TileMapLayer = load(stamp.scene_file_path).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	door.transform = stamp.transform
	if stamps.theme.palette_column_offset != 0:
		for cell in door.get_used_cells():
			door.set_cell(cell, door.get_cell_source_id(cell), stamps.map_coords(door.get_cell_atlas_coords(cell)), door.get_cell_alternative_tile(cell))
	return door


# Returns placement id -> { node, hidden, appears, block_id }.
func _add_placements(room: DMRoomLayout, root: Node2D) -> Dictionary:
	var objects := {}
	for placement in room.placements:
		if placement.scene == null:
			continue
		var object: Node = placement.scene.instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
		object.name = _unique_name(root, object.name)
		for key in placement.overrides:
			if not DMNames.CHILD_OVERRIDES.has(key):
				object.set(key, placement.overrides[key])

		# Enemies always start in the room, so Room's clear tracking sees them.
		var appears := placement.appears
		if DMNames.kind_of(placement.scene) == "enemy":
			appears = Appears.IMMEDIATELY

		var hidden: Node2D = null
		if appears == Appears.IMMEDIATELY:
			object.position = Vector2(placement.position)
			root.add_child(object)
		else:
			hidden = Node2D.new()
			hidden.set_script(load(DMNames.HIDDEN_ITEM_SCRIPT))
			hidden.name = _unique_name(root, "Hidden" + object.name)
			hidden.position = Vector2(placement.position)
			if preview:
				hidden.modulate = Color(1, 1, 1, 0.4)
			root.add_child(hidden)
			hidden.add_child(object)
		_apply_child_overrides(placement, object, root)
		objects[placement.id] = {
			"node": object,
			"hidden": hidden,
			"appears": appears,
			"block_id": placement.block_id,
		}
	return objects


func _wire(room: DMRoomLayout, root: Node2D, shutters: Dictionary, objects: Dictionary) -> void:
	for placement_id in objects:
		var object: Dictionary = objects[placement_id]
		match object.appears:
			Appears.ON_ROOM_CLEARED:
				root.connect(DMNames.ROOM_CLEARED_SIGNAL, Callable(object.hidden, DMNames.FIRE_METHOD), CONNECT_PERSIST)
			Appears.ON_BLOCK_PUSHED:
				var block := _block(objects, object.block_id)
				if block:
					block.connect(DMNames.BLOCK_PUSHED_SIGNAL, Callable(object.hidden, DMNames.FIRE_METHOD), CONNECT_PERSIST)

	for side in shutters:
		var rule := layout.edge_on(room, side).shutter_for(room.id)
		var block := _block(objects, rule.block_id)
		if rule.mode != DMShutterRule.Mode.UNTIL_BLOCK_PUSHED or block == null:
			continue
		var trigger := Node.new()
		trigger.set_script(load(DMNames.DOOR_TRIGGER_SCRIPT))
		trigger.name = "DoorTrigger" + DMDungeonLayout.side_name(side)
		root.add_child(trigger)
		trigger.set(DMNames.TRIGGER_TARGETS, [shutters[side]])
		block.connect(DMNames.BLOCK_PUSHED_SIGNAL, Callable(trigger, DMNames.FIRE_METHOD), CONNECT_PERSIST)

	# Stairs the dungeon scene reveals when a block here is pushed. The room only announces it.
	for warp in layout.warps:
		if warp.from_room_id == room.id and warp.appears == Appears.ON_BLOCK_PUSHED:
			var block := _block(objects, warp.block_id)
			if block and not block.is_connected(DMNames.BLOCK_PUSHED_SIGNAL, Callable(root, DMNames.ROOM_REVEAL_SECRET)):
				block.connect(DMNames.BLOCK_PUSHED_SIGNAL, Callable(root, DMNames.ROOM_REVEAL_SECRET), CONNECT_PERSIST)


# A scene only saves changes to an instance's insides when that instance is editable. It's set per placed
# object, and only for one that overrides something inside it. The room is rebuilt on every export, so the
# saved overrides never go stale.
func _apply_child_overrides(placement: DMPlacement, object: Node, root: Node2D) -> void:
	for key in placement.overrides:
		if not DMNames.CHILD_OVERRIDES.has(key):
			continue
		var target := DMNames.find_by_script(object, DMNames.CHILD_OVERRIDES[key][0])
		if target == null:
			continue
		target.set(DMNames.CHILD_OVERRIDES[key][1], placement.overrides[key])
		root.set_editable_instance(object, true)


# A direct child of the room, so Room fires it on every entry as it does any IResettableEntity.
func _add_entry_text(room: DMRoomLayout, root: Node2D) -> void:
	if room.entry_text.strip_edges().is_empty():
		return
	var trigger := Node.new()
	trigger.set_script(load(DMNames.DIALOGUE_TRIGGER_SCRIPT))
	trigger.name = "DialogueTrigger"
	trigger.set(DMNames.DIALOGUE_TEXT, room.entry_text)
	root.add_child(trigger)


func _block(objects: Dictionary, block_id: int) -> Node:
	if not objects.has(block_id):
		return null
	var node: Node = objects[block_id].node
	return node if node.has_signal(DMNames.BLOCK_PUSHED_SIGNAL) else null


# "Stalfos", "Stalfos2", "Stalfos3", ... as in the hand-made rooms.
static func _unique_name(parent: Node, base: String) -> String:
	var candidate := base
	var n := 2
	while parent.has_node(NodePath(candidate)):
		candidate = "%s%d" % [base, n]
		n += 1
	return candidate


# Owners make PackedScene.pack keep the nodes and their connections. Only an instanced scene's root gets one.
static func _own(node: Node, root: Node) -> void:
	DMNames.tag(node)
	for child in node.get_children():
		child.owner = root
		DMNames.tag(child)
		if child.scene_file_path.is_empty():
			_own(child, root)
