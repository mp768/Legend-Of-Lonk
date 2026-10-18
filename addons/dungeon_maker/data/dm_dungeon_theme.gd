@tool
class_name DMDungeonTheme
extends Resource

# Every tile the builder pastes comes from the stamp scene, so the builder holds no atlas coordinates.
@export var stamps: PackedScene
@export var wall_tile_set: TileSet
@export var low_wall_tile_set: TileSet
# Shifts pasted tiles in atlas columns 0-63 by this much. The atlas holds the dungeon tiles in four colours,
# 16 columns apart.
@export var palette_column_offset: int = 0
# Folders the object palette lists, grouped by folder.
@export var entity_folders: PackedStringArray = []
