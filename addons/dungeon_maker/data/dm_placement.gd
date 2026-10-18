@tool
class_name DMPlacement
extends Resource

# Something placed in a room: an enemy, collectible or world object, at an exact room-local pixel.
enum Appears { IMMEDIATELY, ON_ROOM_CLEARED, ON_BLOCK_PUSHED }

@export var id: int = -1
@export var scene: PackedScene
@export var position: Vector2i
@export var appears: Appears = Appears.IMMEDIATELY
# The pushable block (a placement id in the same room), for ON_BLOCK_PUSHED.
@export var block_id: int = -1
# Property name -> value, applied to the instance, e.g. { "Drops": key_drop.tres }.
@export var overrides: Dictionary = {}
