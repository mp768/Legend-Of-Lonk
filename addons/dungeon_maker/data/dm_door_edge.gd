@tool
class_name DMDoorEdge
extends Resource

# A doorway between two neighbouring rooms, stored once for both sides. A missing edge means a wall, so a
# door can never exist on one side only.
enum Kind { OPEN, LOCKED }

# Stable room ids. room_a is the left or upper room.
@export var room_a: int = -1
@export var room_b: int = -1
# Shared, because a locked door is locked on both sides.
@export var kind: Kind = Kind.OPEN
# Shutters are per side, as Dungeon 1 uses them (room 6's shutter faces a plain doorway in room 5).
@export var shutter_a: DMShutterRule
@export var shutter_b: DMShutterRule


func _init() -> void:
	shutter_a = DMShutterRule.new()
	shutter_b = DMShutterRule.new()


func has_room(room_id: int) -> bool:
	return room_a == room_id or room_b == room_id


func other_room(room_id: int) -> int:
	return room_b if room_id == room_a else room_a


func shutter_for(room_id: int) -> DMShutterRule:
	return shutter_a if room_id == room_a else shutter_b
