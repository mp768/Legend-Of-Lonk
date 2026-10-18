@tool
class_name DMWarpLink
extends Resource

# Stairs from one room to an arrival point in another, optionally with a way back (like the basement ladder).
@export var from_room_id: int = -1
@export var from_position: Vector2i
@export var warp_scene: PackedScene
@export var to_room_id: int = -1
@export var arrival_position: Vector2i
# Lets the stairs appear later, like NES stairs under a pushed block. block_id is a placement in the from room.
@export var appears: DMPlacement.Appears = DMPlacement.Appears.IMMEDIATELY
@export var block_id: int = -1
# Also create the way back: a warp at return_position in the destination room, arriving at
# return_arrival_position in the starting room.
@export var two_way: bool = false
@export var return_position: Vector2i
@export var return_arrival_position: Vector2i
