@tool
class_name DMShutterRule
extends Resource

# What closes a shutter on one side of a doorway, and what opens it again.
enum Mode { NONE, UNTIL_CLEARED, UNTIL_BLOCK_PUSHED }

@export var mode: Mode = Mode.NONE
# The pushable block (a placement id in the same room) that opens the shutter, for UNTIL_BLOCK_PUSHED.
@export var block_id: int = -1
