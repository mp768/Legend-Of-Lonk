@tool
class_name DMDungeonLayout
extends Resource

# The Dungeon Maker's source data. Exporting turns it into the dungeon and room scenes.
enum DoorSide { NONE, UP, DOWN, LEFT, RIGHT }

const SIDE_OFFSETS := {
	DoorSide.UP: Vector2i(0, -1),
	DoorSide.DOWN: Vector2i(0, 1),
	DoorSide.LEFT: Vector2i(-1, 0),
	DoorSide.RIGHT: Vector2i(1, 0),
}

@export var dungeon_name: String = "Dungeon2"
@export_dir var export_dir: String = "res://Assets/Scenes/Dungeons/Dungeon2"
@export var theme: DMDungeonTheme
@export var music: AudioStream
@export var rooms: Array[DMRoomLayout] = []
@export var edges: Array[DMDoorEdge] = []
@export var warps: Array[DMWarpLink] = []
@export var start_room_id: int = -1
@export var entrance_side: DoorSide = DoorSide.NONE
# Where Lonk spawns in the starting room, and where a Wallmaster drags him back to.
@export var entrance_point: Vector2i = Vector2i(128, 136)
@export var next_room_id: int = 0
@export var next_placement_id: int = 0


static func opposite(side: int) -> int:
	match side:
		DoorSide.UP: return DoorSide.DOWN
		DoorSide.DOWN: return DoorSide.UP
		DoorSide.LEFT: return DoorSide.RIGHT
		DoorSide.RIGHT: return DoorSide.LEFT
	return DoorSide.NONE


static func side_name(side: int) -> String:
	return ["None", "Up", "Down", "Left", "Right"][side]


func get_room(room_id: int) -> DMRoomLayout:
	for room in rooms:
		if room.id == room_id:
			return room
	return null


func room_at(grid_pos: Vector2i) -> DMRoomLayout:
	for room in rooms:
		if not room.off_grid and room.grid_pos == grid_pos:
			return room
	return null


func new_room(grid_pos: Vector2i) -> DMRoomLayout:
	var room := DMRoomLayout.new()
	room.id = next_room_id
	next_room_id += 1
	room.room_name = "Room%d" % room.id
	room.grid_pos = grid_pos
	return room


func new_placement_id() -> int:
	next_placement_id += 1
	return next_placement_id - 1


# The side of `room` that faces `other`, or NONE when they aren't grid neighbours.
func side_between(room: DMRoomLayout, other: DMRoomLayout) -> int:
	if room == null or other == null or room.off_grid or other.off_grid:
		return DoorSide.NONE
	for side in SIDE_OFFSETS:
		if room.grid_pos + SIDE_OFFSETS[side] == other.grid_pos:
			return side
	return DoorSide.NONE


func edge_between(room_id_a: int, room_id_b: int) -> DMDoorEdge:
	for edge in edges:
		if edge.has_room(room_id_a) and edge.has_room(room_id_b):
			return edge
	return null


# The edge on one side of a room, if that side has a door.
func edge_on(room: DMRoomLayout, side: int) -> DMDoorEdge:
	if room.off_grid or side == DoorSide.NONE:
		return null
	var neighbour := room_at(room.grid_pos + SIDE_OFFSETS[side])
	if neighbour == null:
		return null
	return edge_between(room.id, neighbour.id)


# Room a is always the left or upper room.
func make_edge(room_one: DMRoomLayout, room_two: DMRoomLayout) -> DMDoorEdge:
	var edge := DMDoorEdge.new()
	var side := side_between(room_one, room_two)
	var first_is_a := side == DoorSide.RIGHT or side == DoorSide.DOWN
	edge.room_a = room_one.id if first_is_a else room_two.id
	edge.room_b = room_two.id if first_is_a else room_one.id
	return edge


# Edges whose rooms are no longer grid neighbours, e.g. after a room moved.
func broken_edges() -> Array[DMDoorEdge]:
	var broken: Array[DMDoorEdge] = []
	for edge in edges:
		if side_between(get_room(edge.room_a), get_room(edge.room_b)) == DoorSide.NONE:
			broken.append(edge)
	return broken
