@tool
class_name DMRoomLayout
extends Resource

const TILES_WIDE := 12
const TILES_HIGH := 7

# Stable, never reused. Edges, warps and the start room refer to rooms by id.
@export var id: int = -1
# Names the exported file and node, so moving a room on the grid never renames it.
@export var room_name: String = ""
@export var grid_pos: Vector2i
# Reached only by warps (basements). Kept in a tray, not on the map.
@export var off_grid: bool = false
# A hand-made room used as is. The builder only wires its neighbours.
@export var custom_scene: PackedScene
# Room.RoomView, e.g. 1 for side-scroll.
@export var view: int = 0
# 12x7 game tiles, each an index into the theme's brush list.
@export var tiles: PackedByteArray
@export var placements: Array[DMPlacement] = []
# Typed out every time the room is entered, like the old man's hint. Empty for none.
@export_multiline var entry_text: String = ""


func _init() -> void:
	tiles = PackedByteArray()
	tiles.resize(TILES_WIDE * TILES_HIGH)


func get_tile(tile: Vector2i) -> int:
	return tiles[tile.y * TILES_WIDE + tile.x]


func get_placement(placement_id: int) -> DMPlacement:
	for placement in placements:
		if placement.id == placement_id:
			return placement
	return null


static func in_bounds(tile: Vector2i) -> bool:
	return tile.x >= 0 and tile.y >= 0 and tile.x < TILES_WIDE and tile.y < TILES_HIGH
