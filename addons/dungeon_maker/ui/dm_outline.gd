@tool
extends VBoxContainer

# Everything in the dungeon as a list: rooms, and inside each room its doors, objects, warps and the entrance.
# Clicking an entry selects it (and focuses its room). The focused room is expanded.

const DoorSide := DMDungeonLayout.DoorSide
const Appears := DMPlacement.Appears

@export var main_path: NodePath
var main: Node

var _tree: Tree
var _syncing := false
var _rebuild_queued := false


func _ready() -> void:
	main = get_node(main_path)
	if main == null or EditorInterface.get_edited_scene_root() == owner:
		return
	var title := Label.new()
	title.text = "Dungeon contents"
	title.tooltip_text = "Every room and what's in it. Click an entry to select and edit it."
	title.mouse_filter = Control.MOUSE_FILTER_PASS
	add_child(title)
	_tree = Tree.new()
	_tree.hide_root = true
	_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_tree.item_selected.connect(_on_selected)
	_tree.item_activated.connect(func(): main.frame_requested.emit())
	add_child(_tree)
	var hint := Label.new()
	hint.text = "Double-click to frame it in the view."
	hint.modulate = Color(1, 1, 1, 0.55)
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(hint)
	main.layout_changed.connect(_queue_rebuild)
	main.selection_changed.connect(_queue_rebuild)
	main.focus_changed.connect(_queue_rebuild)


func _queue_rebuild() -> void:
	if not _rebuild_queued:
		_rebuild_queued = true
		_rebuild.call_deferred()


func _icon(icon_name: String) -> Texture2D:
	return EditorInterface.get_base_control().get_theme_icon(icon_name, "EditorIcons")


func _rebuild() -> void:
	_rebuild_queued = false
	if main.layout == null:
		return
	_syncing = true
	_tree.clear()
	var root := _tree.create_item()
	var dungeon := _item(root, main.layout.dungeon_name, "Node", {}, "The dungeon. Select it to set its name, export folder and music.")
	var grid_rooms: Array = main.layout.rooms.filter(func(r): return not r.off_grid)
	var warp_rooms: Array = main.layout.rooms.filter(func(r): return r.off_grid)
	for room in grid_rooms:
		_room_item(dungeon, room)
	if not warp_rooms.is_empty():
		var group := _item(dungeon, "Warp-only rooms", "Folder", null, "Rooms off the grid, reached only by stairs.")
		for room in warp_rooms:
			_room_item(group, room)
	_select_current(root)
	_syncing = false
	if _tree.get_selected():
		_tree.scroll_to_item.call_deferred(_tree.get_selected())
	else:
		_tree.scroll_to_item.call_deferred(root.get_first_child())


func _room_item(parent: TreeItem, room: DMRoomLayout) -> void:
	var start: bool = room.id == main.layout.start_room_id
	var text := ("★ " if start else "") + room.room_name + ("  (custom scene)" if room.custom_scene else "")
	var tip := "Room %s. Click to edit it." % room.room_name
	if room.custom_scene:
		tip += "\nUses %s as it is." % room.custom_scene.resource_path
	var item := _item(parent, text, "Node2D", { "type": "room", "id": room.id }, tip)
	item.collapsed = main.focused_room_id != room.id

	if not room.off_grid:
		for side in [DoorSide.UP, DoorSide.DOWN, DoorSide.LEFT, DoorSide.RIGHT]:
			_item(item, _door_text(room, side), "Door" if main.door_kind(room, side) != "wall" else "GuiRadioUnchecked", { "type": "door", "room": room.id, "side": side }, _door_tip(room, side))
	if start:
		_item(item, "Lonk starts at (%d, %d)" % [main.layout.entrance_point.x, main.layout.entrance_point.y], "CharacterBody2D", { "type": "entrance" }, "Where Lonk spawns, and where a Wallmaster drags him back to.")
	for placement in room.placements:
		_item(item, _placement_text(room, placement), _kind_icon(placement.scene), { "type": "placement", "room": room.id, "id": placement.id }, placement.scene.resource_path if placement.scene else "Missing scene")
	for warp in main.layout.warps:
		var from: DMRoomLayout = main.layout.get_room(warp.from_room_id)
		var to: DMRoomLayout = main.layout.get_room(warp.to_room_id)
		if warp.from_room_id == room.id:
			_item(item, "Stairs to %s" % (to.room_name if to else "?") + _appears_suffix(room, warp.appears, warp.block_id), "Warp", { "type": "warp", "warp": warp }, "Stairs at (%d, %d)." % [warp.from_position.x, warp.from_position.y])
		elif warp.to_room_id == room.id:
			_item(item, "Arrival from %s" % (from.room_name if from else "?"), "Warp", { "type": "warp", "warp": warp }, "Where the stairs from %s arrive." % (from.room_name if from else "?"))


func _door_text(room: DMRoomLayout, side: int) -> String:
	var kind: String = main.door_kind(room, side)
	var neighbour: DMRoomLayout = main.neighbour_on(room, side)
	var text := "%s: %s" % [DMDungeonLayout.side_name(side), { "wall": "wall", "open": "open", "locked": "locked", "entrance": "dungeon entrance" }[kind]]
	if kind in ["open", "locked"] and neighbour:
		text += " → " + neighbour.room_name
	var rule: DMShutterRule = main.shutter_rule(room, side)
	if rule:
		match rule.mode:
			DMShutterRule.Mode.UNTIL_CLEARED: text += ", shutter until cleared"
			DMShutterRule.Mode.UNTIL_BLOCK_PUSHED: text += ", shutter until block #%d pushed" % rule.block_id
	return text


func _door_tip(room: DMRoomLayout, side: int) -> String:
	if main.neighbour_on(room, side) == null and main.door_kind(room, side) == "wall":
		return "No room on this side. Add one from the overview to put a door here."
	return "Click to choose the door type and its shutter."


func _placement_text(room: DMRoomLayout, placement: DMPlacement) -> String:
	var text: String = "%s (%d, %d)" % [main.placement_label(placement), placement.position.x, placement.position.y]
	if DMNames.kind_of(placement.scene) != "enemy":
		text += _appears_suffix(room, placement.appears, placement.block_id)
	if DMNames.is_block_scene(placement.scene):
		var targets: Array = main.block_targets(room, placement.id)
		if not targets.is_empty():
			text += ", opens %d" % targets.size()
	return text


func _appears_suffix(room: DMRoomLayout, appears: int, block_id: int) -> String:
	match appears:
		Appears.ON_ROOM_CLEARED: return ", appears when cleared"
		Appears.ON_BLOCK_PUSHED: return ", appears when block #%d is pushed" % block_id
	return ""


func _kind_icon(scene: PackedScene) -> String:
	match DMNames.kind_of(scene):
		"enemy": return "Skeleton2D"
		"collectible": return "Area2D"
		"block": return "AnimatableBody2D"
		"warp": return "Warp"
	return "PackedScene"


func _item(parent: TreeItem, text: String, icon_name: String, selection, tip: String) -> TreeItem:
	var item := _tree.create_item(parent)
	item.set_text(0, text)
	item.set_icon(0, _icon(icon_name))
	item.set_tooltip_text(0, tip)
	if selection == null:
		item.set_selectable(0, false)
	else:
		item.set_metadata(0, selection)
	return item


func _select_current(root: TreeItem) -> void:
	var stack: Array[TreeItem] = [root]
	while not stack.is_empty():
		var item: TreeItem = stack.pop_back()
		var meta = item.get_metadata(0)
		if meta is Dictionary and _same(meta, main.selection):
			item.select(0)
			return
		stack.append_array(item.get_children())


func _same(a: Dictionary, b: Dictionary) -> bool:
	if a.get("type") != b.get("type"):
		return false
	match a.get("type", ""):
		"room": return a.id == b.id
		"door": return a.room == b.room and a.side == b.side
		"placement": return a.room == b.room and a.id == b.id
		"warp": return a.warp == b.warp
	return true


func _on_selected() -> void:
	if _syncing:
		return
	var item := _tree.get_selected()
	var meta = item.get_metadata(0) if item else null
	if not meta is Dictionary:
		return
	if meta.get("type") == "room":
		main.focus_room(meta.id)
		main.select(meta)
	else:
		main.select(meta)
