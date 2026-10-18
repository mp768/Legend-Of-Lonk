@tool
class_name DMNames
extends RefCounted

# Every game script, scene and C# member name the builders depend on, in one place. C# members are set by
# their real names: Room's neighbours are lower-case fields, most other exports are PascalCase properties.
# A rename in C# silently breaks export, so the validator checks each of these exists.

const ROOM_SCRIPT := "res://Assets/Scripts/Room.cs"
const ROOM_MANAGER_SCRIPT := "res://Assets/Scripts/RoomManager.cs"
const HIDDEN_ITEM_SCRIPT := "res://Assets/Scripts/HiddenItem.cs"
const DOOR_TRIGGER_SCRIPT := "res://Assets/Scripts/DoorTrigger.cs"
const ENEMY_SCRIPT := "res://Assets/Scripts/Enemy.cs"
const COLLECTIBLE_SCRIPT := "res://Assets/Scripts/Collectible.cs"
const BLOCK_SCRIPT := "res://Assets/Scripts/PushableBlock.cs"
const WARP_SCRIPT := "res://Assets/Scripts/RoomWarp.cs"
const LOCKED_DOOR_SCRIPT := "res://Assets/Scripts/LockedDoor.cs"
const SHUTTER_SCRIPT := "res://Assets/Scripts/ShutterDoor.cs"
const DOOR_SCRIPT := "res://Assets/Scripts/Door.cs"
const DIALOGUE_TRIGGER_SCRIPT := "res://Assets/Scripts/DialogueTrigger.cs"
const LUNGE_STATE_SCRIPT := "res://Assets/Scripts/PushdownAutomata/States/LungeState.cs"

const LONK_SCENE := "res://Assets/Scenes/lonk.tscn"
const STAIRS_SCENE := "res://Assets/Scenes/World/stairs.tscn"
const EAGLE_THEME := "res://addons/dungeon_maker/themes/eagle/eagle_theme.tres"

const LAYERS := ["GroundLayer", "WallLayer", "LowWallLayer", "OverheadLayer"]
const WALL_GROUP := "wall"
const GENERATED_META := &"dungeon_maker_generated"

# Room
const ROOM_NEIGHBOURS := {
	DMDungeonLayout.DoorSide.UP: "upRoom",
	DMDungeonLayout.DoorSide.DOWN: "downRoom",
	DMDungeonLayout.DoorSide.LEFT: "leftRoom",
	DMDungeonLayout.DoorSide.RIGHT: "rightRoom",
}
const ROOM_LOCK_UNTIL_CLEARED := "LockUntilCleared"
const ROOM_VIEW := "View"
const ROOM_CLEARED_SIGNAL := "Cleared"
const ROOM_SECRET_SIGNAL := "SecretRevealed"
const ROOM_REVEAL_SECRET := "RevealSecret"

# RoomManager
const MANAGER_CAMERA := "MainCamera"
const MANAGER_START := "StartingRoom"
const MANAGER_ENTRANCE := "EntrancePoint"
const MANAGER_MUSIC := "Music"

# Doors, triggers and hidden items
const DOOR_STARTS_OPEN := "StartsOpen"
const DOOR_SIDE := "Side"
const SHUTTER_OPENS_WHEN_CLEARED := "OpensWhenCleared"
const TRIGGER_TARGETS := "Targets"
const FIRE_METHOD := "Fire"
const BLOCK_PUSHED_SIGNAL := "Pushed"
const WARP_DESTINATION := "Destination"
const WARP_ARRIVAL := "Arrival"
const DIALOGUE_TEXT := "Text"

# Exits: name, centre of the 16x16 shape.
const EXITS := {
	DMDungeonLayout.DoorSide.LEFT: ["LeftExit", Vector2(16, 88)],
	DMDungeonLayout.DoorSide.RIGHT: ["RightExit", Vector2(240, 88)],
	DMDungeonLayout.DoorSide.UP: ["UpExit", Vector2(128, 16)],
	DMDungeonLayout.DoorSide.DOWN: ["DownExit", Vector2(128, 160)],
}
const PLAYER_BODY_LAYER := 16

const SCREEN_SIZE := Vector2i(256, 176)
const CELL_SIZE := 8
const TILE_SIZE := 16
# The first interior cell. A game tile is 2x2 cells.
const INTERIOR_ORIGIN := Vector2i(4, 4)
# Where placed objects may go: inside the outer walls, including the strip under the top wall.
const PLACEABLE_AREA := Rect2i(32, 32, 192, 112)
const STRIP_ROW := 4

# Overrides the object inspector offers, and the scene kinds they apply to.
const OVERRIDES := {
	"Drops": ["enemy"],
	"CountsTowardClear": ["enemy"],
	"AllowedDirection": ["block"],
	"Value": ["collectible"],
}

# Overrides for values that live on a node inside the placed scene rather than its root, like a blade trap's
# speeds on its LungeState. The node is found by its script, so the scene can arrange its children freely.
# Key -> [script, property]. Offered for any scene that has a node with that script.
const CHILD_OVERRIDES := {
	"LungeSpeed": [LUNGE_STATE_SCRIPT, "LungeSpeed"],
	"ReturnSpeed": [LUNGE_STATE_SCRIPT, "ReturnSpeed"],
}


# The root script of a scene, read without instancing it.
static func root_script_path(scene: PackedScene) -> String:
	if scene == null:
		return ""
	var state := scene.get_state()
	for i in state.get_node_property_count(0):
		if state.get_node_property_name(0, i) == &"script":
			var script: Script = state.get_node_property_value(0, i)
			return script.resource_path if script else ""
	# An inherited or instanced root keeps its script in the base scene.
	var base := state.get_node_instance(0)
	return root_script_path(base) if base else ""


# A property set on a scene's root, read without instancing it. Falls back to the base scene of an
# inherited root.
static func root_property(scene: PackedScene, property: StringName, default: Variant = null) -> Variant:
	if scene == null:
		return default
	var state := scene.get_state()
	for i in state.get_node_property_count(0):
		if state.get_node_property_name(0, i) == property:
			return state.get_node_property_value(0, i)
	var base := state.get_node_instance(0)
	return root_property(base, property, default) if base else default


# A property of the first node in a scene with the given script, read without instancing it. Null if the scene
# has no such node. Looks into the base scene of an inherited root too.
static func child_property(scene: PackedScene, script_path: String, property: StringName) -> Variant:
	if scene == null:
		return null
	var state := scene.get_state()
	for node in state.get_node_count():
		var found := false
		var value: Variant = null
		for i in state.get_node_property_count(node):
			var name := state.get_node_property_name(node, i)
			if name == &"script":
				var script: Script = state.get_node_property_value(node, i)
				found = script != null and script.resource_path == script_path
			elif name == property:
				value = state.get_node_property_value(node, i)
		if found:
			# A property left at the script's default isn't stored in the scene.
			if value == null:
				var script: Script = load(script_path)
				value = script.get_property_default_value(property) if script else null
			return value
	var base := state.get_node_instance(0)
	return child_property(base, script_path, property) if base else null


# The first node in `root`'s subtree, itself included, with the given script.
static func find_by_script(root: Node, script_path: String) -> Node:
	var script: Script = root.get_script()
	if script and script.resource_path == script_path:
		return root
	for child in root.get_children():
		var found := find_by_script(child, script_path)
		if found:
			return found
	return null


# "enemy", "collectible", "block", "warp" or "other".
static func kind_of(scene: PackedScene) -> String:
	match root_script_path(scene):
		ENEMY_SCRIPT: return "enemy"
		COLLECTIBLE_SCRIPT: return "collectible"
		BLOCK_SCRIPT: return "block"
		WARP_SCRIPT: return "warp"
	return "other"


static func is_block_scene(scene: PackedScene) -> bool:
	return kind_of(scene) == "block"


static func is_block(room: DMRoomLayout, placement_id: int) -> bool:
	var placement := room.get_placement(placement_id) if room else null
	return placement != null and is_block_scene(placement.scene)


static func tag(node: Node) -> void:
	node.set_meta(GENERATED_META, true)


static func is_tagged(node: Node) -> bool:
	return node.has_meta(GENERATED_META)


static func file_name(name: String) -> String:
	return name.strip_edges().to_snake_case().validate_filename().replace(" ", "_")


static func node_name(name: String) -> String:
	return name.strip_edges().validate_node_name().replace(" ", "")


# Gives a new resource file a UID when it was saved outside the editor, which doesn't assign one.
static func ensure_uid(path: String, keep_uid: int = ResourceUID.INVALID_ID) -> void:
	var uid := ResourceLoader.get_resource_uid(path)
	var wanted := keep_uid if keep_uid != ResourceUID.INVALID_ID else uid
	if wanted == ResourceUID.INVALID_ID:
		wanted = ResourceUID.create_id()
	if uid != wanted:
		ResourceSaver.set_uid(path, wanted)
	if not ResourceUID.has_id(wanted):
		ResourceUID.add_id(wanted, path)
