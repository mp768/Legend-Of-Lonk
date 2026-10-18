@tool
class_name DMDungeonBuilder
extends RefCounted

# Turns a DMDungeonLayout into the dungeon scene: rooms instanced and positioned, neighbours, stairs, the
# entrance, Lonk and the camera. Everything here points at rooms (direct children) or at nodes this scene adds
# under them, never at nodes inside a room scene.

const DoorSide := DMDungeonLayout.DoorSide
const Appears := DMPlacement.Appears


# room_scenes: room id -> exported PackedScene. Custom rooms use their own scene.
static func build(layout: DMDungeonLayout, room_scenes: Dictionary) -> Node:
	var root := Node.new()
	root.name = DMNames.node_name(layout.dungeon_name)
	root.set_script(load(DMNames.ROOM_MANAGER_SCRIPT))
	root.set(DMNames.MANAGER_MUSIC, layout.music)
	var created: Array[Node] = []

	var rooms := _add_rooms(layout, room_scenes, root, created)
	_set_neighbours(layout, rooms)

	var start: Node2D = rooms.get(layout.start_room_id)
	if start:
		if layout.entrance_side != DoorSide.NONE:
			# A room that lists itself as a neighbour blocks that exit.
			start.set(DMNames.ROOM_NEIGHBOURS[layout.entrance_side], start)
		var entrance := _add_marker(start, "EntrancePoint", layout.entrance_point, created)
		root.set(DMNames.MANAGER_START, start)
		root.set(DMNames.MANAGER_ENTRANCE, entrance)

	for i in layout.warps.size():
		_add_warp(layout.warps[i], i, rooms, created)

	var lonk: Node2D = load(DMNames.LONK_SCENE).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	lonk.name = "Lonk"
	if start:
		lonk.position = start.position + Vector2(layout.entrance_point)
	root.add_child(lonk)
	created.append(lonk)

	var camera := Camera2D.new()
	camera.name = "Camera2D"
	root.add_child(camera)
	created.append(camera)
	root.set(DMNames.MANAGER_CAMERA, camera)

	DMNames.tag(root)
	for node in created:
		node.owner = root
		DMNames.tag(node)
	return root


static func room_position(layout: DMDungeonLayout, room: DMRoomLayout) -> Vector2i:
	if not room.off_grid:
		return room.grid_pos
	# Off-grid rooms are only reached by hard-cut warps, so they just sit in a row below the grid, out of the
	# way of scrolling transitions.
	var bounds := grid_bounds(layout)
	var index := 0
	for other in layout.rooms:
		if other == room:
			break
		if other.off_grid:
			index += 1
	return Vector2i(bounds.position.x + index, bounds.end.y + 1)


static func grid_bounds(layout: DMDungeonLayout) -> Rect2i:
	var bounds := Rect2i()
	var first := true
	for room in layout.rooms:
		if room.off_grid:
			continue
		var cell := Rect2i(room.grid_pos, Vector2i.ONE)
		bounds = cell if first else bounds.merge(cell)
		first = false
	return bounds


static func _add_rooms(layout: DMDungeonLayout, room_scenes: Dictionary, root: Node, created: Array[Node]) -> Dictionary:
	var rooms := {}
	for room in layout.rooms:
		var scene: PackedScene = room.custom_scene if room.custom_scene else room_scenes.get(room.id)
		if scene == null:
			continue
		var instance: Node2D = scene.instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
		instance.name = DMNames.node_name(room.room_name)
		instance.position = Vector2(room_position(layout, room) * DMNames.SCREEN_SIZE)
		root.add_child(instance)
		created.append(instance)
		rooms[room.id] = instance
	return rooms


# Neighbours drive room transitions and keep both halves of a locked door in sync, so both directions are set.
static func _set_neighbours(layout: DMDungeonLayout, rooms: Dictionary) -> void:
	for edge in layout.edges:
		var side := layout.side_between(layout.get_room(edge.room_a), layout.get_room(edge.room_b))
		if side == DoorSide.NONE or not rooms.has(edge.room_a) or not rooms.has(edge.room_b):
			continue
		rooms[edge.room_a].set(DMNames.ROOM_NEIGHBOURS[side], rooms[edge.room_b])
		rooms[edge.room_b].set(DMNames.ROOM_NEIGHBOURS[DMDungeonLayout.opposite(side)], rooms[edge.room_a])


static func _add_marker(parent: Node, marker_name: String, position: Vector2i, created: Array[Node]) -> Marker2D:
	var marker := Marker2D.new()
	marker.name = marker_name
	marker.position = Vector2(position)
	parent.add_child(marker)
	created.append(marker)
	return marker


static func _add_warp(warp: DMWarpLink, index: int, rooms: Dictionary, created: Array[Node]) -> void:
	var from: Node2D = rooms.get(warp.from_room_id)
	var to: Node2D = rooms.get(warp.to_room_id)
	if from == null or to == null or warp.warp_scene == null:
		return
	var arrival := _add_marker(to, "WarpArrival%d" % index, warp.arrival_position, created)
	var stairs: Node2D = warp.warp_scene.instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	stairs.name = "Warp%d" % index
	stairs.set(DMNames.WARP_DESTINATION, to)
	stairs.set(DMNames.WARP_ARRIVAL, arrival)

	if warp.appears == Appears.IMMEDIATELY:
		stairs.position = Vector2(warp.from_position)
		from.add_child(stairs)
	else:
		var hidden := Node2D.new()
		hidden.set_script(load(DMNames.HIDDEN_ITEM_SCRIPT))
		hidden.name = "HiddenWarp%d" % index
		hidden.position = Vector2(warp.from_position)
		from.add_child(hidden)
		created.append(hidden)
		hidden.add_child(stairs)
		var reveal := DMNames.ROOM_CLEARED_SIGNAL if warp.appears == Appears.ON_ROOM_CLEARED else DMNames.ROOM_SECRET_SIGNAL
		from.connect(reveal, Callable(hidden, DMNames.FIRE_METHOD), CONNECT_PERSIST)
	created.append(stairs)

	if warp.two_way:
		var back_arrival := _add_marker(from, "WarpReturnArrival%d" % index, warp.return_arrival_position, created)
		var back: Node2D = warp.warp_scene.instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
		back.name = "ReturnWarp%d" % index
		back.position = Vector2(warp.return_position)
		back.set(DMNames.WARP_DESTINATION, from)
		back.set(DMNames.WARP_ARRIVAL, back_arrival)
		to.add_child(back)
		created.append(back)
