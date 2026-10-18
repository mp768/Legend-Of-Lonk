@tool
extends VBoxContainer

# The Dungeon Maker main screen. Owns the open layout, the selection, which room is in focus and the undo
# history. The world view, the outline and the inspector read from here and change the layout only through the
# edit methods below, so every change is one undoable action.

signal layout_changed
signal selection_changed
# The focused room changed, or the view went back to the whole-dungeon overview (focused_room_id == -1).
signal focus_changed
signal tool_changed
# Asks the world view to frame a room or the whole dungeon without changing focus.
signal frame_requested

enum Tool { SELECT, PAINT, RECT, ERASE, PLACE, WARP }

const TOOL_HELP := {
	Tool.SELECT: "Select and drag objects, doors, warps and the entrance. Arrow keys nudge by 1 px, Delete removes.",
	Tool.PAINT: "Paint the selected brush onto floor tiles. Drag to paint a stroke.",
	Tool.RECT: "Drag a rectangle to fill it with the selected brush.",
	Tool.ERASE: "Paint plain floor. Drag to erase a stroke.",
	Tool.PLACE: "Click in the room to place the object. Hold Shift to keep placing, Esc to stop.",
	Tool.WARP: "Click where the stairs go, then click the arrival point in another room.",
}

var undo: EditorUndoRedoManager
var layout: DMDungeonLayout
var stamps: DMThemeStamps
var dirty := false
# { type = "room" | "door" | "placement" | "warp" | "entrance", ... } or empty for the dungeon itself.
var selection := {}
# The room being edited, or -1 for the overview of the whole dungeon.
var focused_room_id := -1
var tool := Tool.SELECT
var brush_index := 0
# The scene the Place tool puts down.
var object_scene: PackedScene
var snap_collectibles := false
var issues: Array[Dictionary] = []
# While set, the next click on a matching object in the focused room answers it instead of selecting:
# { prompt, kind, callback }.
var pick := {}

@onready var _new_button: Button = %NewButton
@onready var _open_button: Button = %OpenButton
@onready var _save_button: Button = %SaveButton
@onready var _save_as_button: Button = %SaveAsButton
@onready var _undo_button: Button = %UndoButton
@onready var _redo_button: Button = %RedoButton
@onready var _export_button: Button = %ExportButton
@onready var _validate_button: Button = %ValidateButton
@onready var _theme_slot: HBoxContainer = %ThemeSlot
@onready var _status: Label = %StatusLabel
@onready var _issues_list: ItemList = %IssuesList

var _theme_picker: EditorResourcePicker
var _file_dialog: EditorFileDialog
var _confirm: ConfirmationDialog
var _pending_confirm: Callable
var _pending_open: DMDungeonLayout


func _ready() -> void:
	if EditorInterface.get_edited_scene_root() == self:
		return
	var editor := EditorInterface.get_base_control()
	# Minimum sizes in the scene are in unscaled pixels, so panels keep their room on a HiDPI screen.
	var ui_scale := EditorInterface.get_editor_scale()
	for path in ["Body/Outline", "Body/Center/Side", "Body/Center/Side/Palette", "%IssuesList"]:
		var control: Control = get_node(path)
		control.custom_minimum_size *= ui_scale
	_new_button.icon = editor.get_theme_icon("New", "EditorIcons")
	_open_button.icon = editor.get_theme_icon("Load", "EditorIcons")
	_save_button.icon = editor.get_theme_icon("Save", "EditorIcons")
	_undo_button.icon = editor.get_theme_icon("UndoRedo", "EditorIcons")
	_redo_button.icon = editor.get_theme_icon("Redo", "EditorIcons")
	_export_button.icon = editor.get_theme_icon("PackedScene", "EditorIcons")
	_validate_button.icon = editor.get_theme_icon("StatusWarning", "EditorIcons")

	_new_button.pressed.connect(func(): _ask_discard(new_layout))
	_open_button.pressed.connect(func(): _ask_discard(_show_open_dialog))
	_save_button.pressed.connect(save_or_ask)
	_save_as_button.pressed.connect(_show_save_dialog)
	_undo_button.pressed.connect(func(): _history().undo())
	_redo_button.pressed.connect(func(): _history().redo())
	_export_button.pressed.connect(export_layout)
	_validate_button.pressed.connect(validate)
	_issues_list.item_selected.connect(_on_issue_selected)

	_theme_picker = EditorResourcePicker.new()
	_theme_picker.base_type = "Resource"
	_theme_picker.custom_minimum_size.x = 160
	_theme_picker.tooltip_text = "The theme the rooms are built from: its stamp scene holds every tile, door and brush."
	_theme_picker.resource_changed.connect(_on_theme_picked)
	_theme_slot.add_child(_theme_picker)

	_file_dialog = EditorFileDialog.new()
	_file_dialog.access = EditorFileDialog.ACCESS_RESOURCES
	_file_dialog.add_filter("*.tres", "Dungeon layout")
	add_child(_file_dialog)

	_confirm = ConfirmationDialog.new()
	_confirm.confirmed.connect(func():
		var then := _pending_confirm
		_pending_confirm = Callable()
		if then.is_valid():
			then.call())
	# A cancelled question changes nothing, but anything showing a half-made choice is redrawn from the layout.
	_confirm.canceled.connect(func():
		_pending_confirm = Callable()
		selection_changed.emit())
	add_child(_confirm)

	new_layout()
	dirty = false


# ---- Layout lifetime ----

func new_layout() -> void:
	var fresh := DMDungeonLayout.new()
	fresh.theme = load(DMNames.EAGLE_THEME)
	var room := fresh.new_room(Vector2i.ZERO)
	room.room_name = "Entrance"
	fresh.rooms.append(room)
	fresh.start_room_id = room.id
	fresh.entrance_side = DMDungeonLayout.DoorSide.DOWN
	_set_layout(fresh)
	dirty = true


# Called by the plugin when a layout is double-clicked.
func request_open(target: DMDungeonLayout) -> void:
	if target == layout:
		return
	_pending_open = target
	_ask_discard(func(): _set_layout(_pending_open))


func _set_layout(target: DMDungeonLayout) -> void:
	layout = target
	if layout.theme == null:
		layout.theme = load(DMNames.EAGLE_THEME)
	_reload_stamps()
	issues.clear()
	_issues_list.clear()
	dirty = false
	selection = {}
	focused_room_id = -1
	pick = {}
	tool = Tool.SELECT
	_theme_picker.edited_resource = layout.theme
	set_status("Opened %s" % (layout.resource_path if not layout.resource_path.is_empty() else "a new layout"))
	layout_changed.emit()
	focus_changed.emit()
	selection_changed.emit()
	tool_changed.emit()


func _reload_stamps() -> void:
	if stamps:
		stamps.free_stamps()
	stamps = DMThemeStamps.new(layout.theme) if layout.theme and layout.theme.stamps else null


func _exit_tree() -> void:
	if stamps:
		stamps.free_stamps()
		stamps = null


func save_or_ask() -> void:
	if layout.resource_path.is_empty():
		_show_save_dialog()
	else:
		save_layout(layout.resource_path)


func save_layout(path: String) -> void:
	var error := ResourceSaver.save(layout, path)
	if error != OK:
		set_status("Couldn't save %s: %s" % [path, error_string(error)])
		return
	layout.take_over_path(path)
	dirty = false
	EditorInterface.get_resource_filesystem().update_file(path)
	set_status("Saved %s" % path)


func _show_open_dialog() -> void:
	_file_dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_FILE
	_file_dialog.title = "Open a dungeon layout"
	_reconnect_dialog(func(path): _open_path(path))
	_file_dialog.popup_file_dialog()


func _show_save_dialog() -> void:
	_file_dialog.file_mode = EditorFileDialog.FILE_MODE_SAVE_FILE
	_file_dialog.title = "Save the dungeon layout"
	_file_dialog.current_file = DMNames.file_name(layout.dungeon_name) + ".dungeon.tres"
	_reconnect_dialog(func(path): save_layout(path))
	_file_dialog.popup_file_dialog()


func _reconnect_dialog(callback: Callable) -> void:
	for connection in _file_dialog.file_selected.get_connections():
		_file_dialog.file_selected.disconnect(connection.callable)
	_file_dialog.file_selected.connect(callback)


func _open_path(path: String) -> void:
	var loaded = load(path)
	if loaded is DMDungeonLayout:
		_set_layout(loaded)
	else:
		set_status("%s isn't a dungeon layout." % path)


func _ask_discard(then: Callable) -> void:
	if not dirty:
		then.call()
		return
	ask("The layout has unsaved changes. Discard them?", then)


func ask(question: String, then: Callable) -> void:
	_pending_confirm = then
	_confirm.dialog_text = question
	_confirm.popup_centered()


func set_status(text: String) -> void:
	if _status:
		_status.text = text
		_status.tooltip_text = text


func _on_theme_picked(resource: Resource) -> void:
	if resource is DMDungeonTheme:
		commit("Change theme", [[layout, "theme", layout.theme, resource]])
	else:
		_theme_picker.edited_resource = layout.theme
		set_status("A theme must be a DMDungeonTheme resource.")


func _history() -> UndoRedo:
	return undo.get_history_undo_redo(EditorUndoRedoManager.GLOBAL_HISTORY)


# ---- Export and validation ----

func validate() -> void:
	issues = DMValidator.validate(layout)
	_show_issues()
	set_status("%d problems found." % issues.size() if not issues.is_empty() else "No problems found.")


func export_layout() -> void:
	var result := DMExporter.export_dungeon(layout)
	issues = result.issues
	_show_issues()
	if not result.ok:
		set_status("Export failed. See the problems list.")
		return
	set_status("Exported to %s: %d written, %d unchanged." % [layout.export_dir, result.written.size(), result.unchanged.size()])
	if not result.orphans.is_empty():
		var orphans: Array = result.orphans
		ask("These room files no longer belong to any room:\n%s\n\nDelete them?" % "\n".join(orphans), func():
			for path in orphans:
				DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
			EditorInterface.get_resource_filesystem().scan())


func _show_issues() -> void:
	_issues_list.clear()
	var editor := EditorInterface.get_base_control()
	for issue in issues:
		var icon := editor.get_theme_icon("StatusError" if issue.severity == "error" else "StatusWarning", "EditorIcons")
		var index := _issues_list.add_item(issue.message, icon)
		_issues_list.set_item_tooltip(index, issue.message + "\nClick to show it.")


func _on_issue_selected(index: int) -> void:
	var issue: Dictionary = issues[index]
	if issue.placement_id != -1:
		select({ "type": "placement", "room": issue.room_id, "id": issue.placement_id })
	elif issue.edge:
		var a := layout.get_room(issue.edge.room_a)
		var side := layout.side_between(a, layout.get_room(issue.edge.room_b))
		var room_id: int = issue.room_id if issue.room_id != -1 else a.id
		if room_id != a.id:
			side = DMDungeonLayout.opposite(side)
		select({ "type": "door", "room": room_id, "side": side })
	elif issue.room_id != -1:
		select({ "type": "room", "id": issue.room_id })


# ---- Focus, tools and selection ----

func focused_room() -> DMRoomLayout:
	return layout.get_room(focused_room_id) if layout else null


func focus_room(room_id: int) -> void:
	if layout.get_room(room_id) == null:
		return
	if focused_room_id != room_id:
		focused_room_id = room_id
		focus_changed.emit()
	if not _selection_in_room(room_id):
		select({ "type": "room", "id": room_id })


# Back to the whole dungeon. Esc does this.
func show_overview() -> void:
	set_tool(Tool.SELECT)
	pick = {}
	focused_room_id = -1
	focus_changed.emit()


func set_tool(new_tool: int, scene: PackedScene = null) -> void:
	tool = new_tool
	if new_tool == Tool.PLACE:
		object_scene = scene
	tool_changed.emit()


func start_pick(prompt: String, kind: String, callback: Callable) -> void:
	pick = { "prompt": prompt, "kind": kind, "callback": callback }
	set_status(prompt + " Esc cancels.")
	tool_changed.emit()


func finish_pick(value) -> void:
	var callback: Callable = pick.callback
	pick = {}
	tool_changed.emit()
	callback.call(value)


# Selecting something inside a room also focuses that room, so the world view shows it.
func select(target: Dictionary) -> void:
	selection = target
	var room_id := _room_of(target)
	if room_id != -1 and room_id != focused_room_id and layout.get_room(room_id):
		focused_room_id = room_id
		focus_changed.emit()
	selection_changed.emit()


func _room_of(target: Dictionary) -> int:
	match target.get("type", ""):
		"placement", "door": return target.room
		"warp": return focused_room_id if focused_room_id in [target.warp.from_room_id, target.warp.to_room_id] else target.warp.from_room_id
		"entrance": return layout.start_room_id
		"room": return focused_room_id if focused_room_id == target.id else -1
	return -1


func _selection_in_room(room_id: int) -> bool:
	return _room_of(selection) == room_id or (selection.get("type") == "room" and selection.id == room_id)


func selected_placement() -> DMPlacement:
	if selection.get("type") != "placement":
		return null
	var room := layout.get_room(selection.room)
	return room.get_placement(selection.id) if room else null


func placement_label(placement: DMPlacement) -> String:
	return placement.scene.resource_path.get_file().get_basename().capitalize() if placement.scene else "Missing scene"


# ---- Undoable edits ----

# Each change is [object, property, before, after]. Arrays and dictionaries are always replaced, never changed
# in place, so the values held by the history stay as they were.
func commit(action: String, changes: Array) -> void:
	if undo == null:
		for change in changes:
			change[0].set(change[1], change[3])
		_after_history()
		return
	# This screen as the context puts every action in the global history, whichever scene is open. Values are
	# set through this screen too: a property change on an unsaved resource would otherwise be filed under the
	# open scene's history.
	undo.create_action("Dungeon: " + action, UndoRedo.MERGE_DISABLE, self)
	for change in changes:
		undo.add_do_method(self, &"_apply", change[0], change[1], change[3])
	for i in range(changes.size() - 1, -1, -1):
		undo.add_undo_method(self, &"_apply", changes[i][0], changes[i][1], changes[i][2])
	undo.add_do_method(self, &"_after_history")
	undo.add_undo_method(self, &"_after_history")
	undo.commit_action()


func _apply(object: Object, property: String, value: Variant) -> void:
	object.set(property, value)


func _after_history() -> void:
	dirty = true
	if stamps == null or stamps.theme != layout.theme:
		_reload_stamps()
	if focused_room_id != -1 and layout.get_room(focused_room_id) == null:
		focused_room_id = -1
		focus_changed.emit()
	if not _selection_exists():
		selection = {}
		selection_changed.emit()
	layout_changed.emit()
	selection_changed.emit()


func _selection_exists() -> bool:
	match selection.get("type", ""):
		"room": return layout.get_room(selection.id) != null
		"door": return layout.get_room(selection.room) != null
		"placement": return selected_placement() != null
		"warp": return selection.warp in layout.warps
	return true


func with_item(array: Array, item) -> Array:
	var copy := array.duplicate()
	copy.append(item)
	return copy


func without_item(array: Array, item) -> Array:
	var copy := array.duplicate()
	copy.erase(item)
	return copy


func add_room(grid_pos: Vector2i) -> void:
	if layout.room_at(grid_pos) != null:
		return
	var room := layout.new_room(grid_pos)
	room.room_name = unique_room_name("Room%d" % room.id)
	var changes := [
		[layout, "rooms", layout.rooms, with_item(layout.rooms, room)],
		[layout, "next_room_id", room.id, layout.next_room_id],
	]
	if layout.get_room(layout.start_room_id) == null:
		changes.append([layout, "start_room_id", layout.start_room_id, room.id])
	commit("Add room", changes)
	select({ "type": "room", "id": room.id })


func delete_room(room_id: int) -> void:
	var room := layout.get_room(room_id)
	if room == null:
		return
	var edges := layout.edges.filter(func(e): return not e.has_room(room_id))
	var warps := layout.warps.filter(func(w): return w.from_room_id != room_id and w.to_room_id != room_id)
	var changes := [
		[layout, "rooms", layout.rooms, without_item(layout.rooms, room)],
		[layout, "edges", layout.edges, edges],
		[layout, "warps", layout.warps, warps],
	]
	if layout.start_room_id == room_id:
		changes.append([layout, "start_room_id", room_id, -1])
	commit("Delete room", changes)


func duplicate_room(room_id: int) -> void:
	var room := layout.get_room(room_id)
	var copy: DMRoomLayout = room.duplicate(true)
	copy.id = layout.next_room_id
	copy.room_name = unique_room_name(room.room_name + "Copy")
	copy.grid_pos = free_cell_near(room.grid_pos)
	var next_id := layout.next_placement_id
	var id_map := {}
	for placement in copy.placements:
		id_map[placement.id] = next_id
		placement.id = next_id
		next_id += 1
	for placement in copy.placements:
		placement.block_id = id_map.get(placement.block_id, -1)
	commit("Duplicate room", [
		[layout, "rooms", layout.rooms, with_item(layout.rooms, copy)],
		[layout, "next_room_id", layout.next_room_id, copy.id + 1],
		[layout, "next_placement_id", layout.next_placement_id, next_id],
	])
	select({ "type": "room", "id": copy.id })


func rename_room(room_id: int, new_name: String) -> void:
	var room := layout.get_room(room_id)
	new_name = DMNames.node_name(new_name)
	if room == null or new_name.is_empty() or new_name == room.room_name:
		return
	commit("Rename room", [[room, "room_name", room.room_name, unique_room_name(new_name, room)]])


func unique_room_name(base: String, except: DMRoomLayout = null) -> String:
	var candidate := base
	var n := 2
	while layout.rooms.any(func(r): return r != except and DMNames.file_name(r.room_name) == DMNames.file_name(candidate)):
		candidate = "%s%d" % [base, n]
		n += 1
	return candidate


func free_cell_near(cell: Vector2i) -> Vector2i:
	for distance in range(1, 64):
		for offset in [Vector2i(distance, 0), Vector2i(0, distance), Vector2i(-distance, 0), Vector2i(0, -distance)]:
			if layout.room_at(cell + offset) == null:
				return cell + offset
	return cell + Vector2i(64, 0)


# Doors that moving a room would leave without a neighbour.
func edges_broken_by_move(room: DMRoomLayout, grid_pos: Vector2i, off_grid: bool) -> Array[DMDoorEdge]:
	var before_pos := room.grid_pos
	var before_off := room.off_grid
	room.grid_pos = grid_pos
	room.off_grid = off_grid
	var broken := layout.broken_edges()
	room.grid_pos = before_pos
	room.off_grid = before_off
	return broken.filter(func(e): return e.has_room(room.id))


# Moving a room drops doors that no longer join neighbours, after asking. Warp-only rooms sit off the grid.
func move_room(room_id: int, grid_pos: Vector2i, off_grid: bool) -> void:
	var room := layout.get_room(room_id)
	if room == null or (not off_grid and layout.room_at(grid_pos) not in [null, room]):
		return
	if room.grid_pos == grid_pos and room.off_grid == off_grid:
		return
	var broken := edges_broken_by_move(room, grid_pos, off_grid)
	var changes := [
		[room, "grid_pos", room.grid_pos, grid_pos],
		[room, "off_grid", room.off_grid, off_grid],
	]
	if layout.start_room_id == room_id and off_grid and layout.entrance_side != DMDungeonLayout.DoorSide.NONE:
		changes.append([layout, "entrance_side", layout.entrance_side, DMDungeonLayout.DoorSide.NONE])
	var apply := func():
		changes.append([layout, "edges", layout.edges, layout.edges.filter(func(e): return not e in broken)])
		commit("Move room" if not off_grid else "Make room warp-only", changes)
	if broken.is_empty():
		apply.call()
	else:
		ask("This removes %d door(s) of %s that would no longer lead to a neighbouring room. Continue?" % [broken.size(), room.room_name], apply)


func set_start(room_id: int) -> void:
	commit("Set start room", [[layout, "start_room_id", layout.start_room_id, room_id]])


func set_property(action: String, object: Object, property: String, value) -> void:
	var before = object.get(property)
	if typeof(before) == typeof(value) and before == value:
		return
	commit(action, [[object, property, before, value]])


# ---- Doors ----

func neighbour_on(room: DMRoomLayout, side: int) -> DMRoomLayout:
	if room == null or room.off_grid or side == DMDungeonLayout.DoorSide.NONE:
		return null
	return layout.room_at(room.grid_pos + DMDungeonLayout.SIDE_OFFSETS[side])


# "wall", "open", "locked" or "entrance" for one side of a room.
func door_kind(room: DMRoomLayout, side: int) -> String:
	var edge := layout.edge_on(room, side)
	if edge:
		return "locked" if edge.kind == DMDoorEdge.Kind.LOCKED else "open"
	if room.id == layout.start_room_id and layout.entrance_side == side:
		return "entrance"
	return "wall"


# One action for the whole doorway: an edge is stored once, so both rooms change together.
func set_door(room: DMRoomLayout, side: int, kind: String) -> void:
	var current := door_kind(room, side)
	if kind == current:
		return
	var changes := []
	var neighbour := neighbour_on(room, side)
	var edge := layout.edge_on(room, side)
	if current == "entrance":
		changes.append([layout, "entrance_side", layout.entrance_side, DMDungeonLayout.DoorSide.NONE])
	match kind:
		"wall":
			if edge:
				changes.append([layout, "edges", layout.edges, without_item(layout.edges, edge)])
		"entrance":
			if edge:
				changes.append([layout, "edges", layout.edges, without_item(layout.edges, edge)])
			if layout.start_room_id != room.id:
				changes.append([layout, "start_room_id", layout.start_room_id, room.id])
			changes.append([layout, "entrance_side", layout.entrance_side, side])
		"open", "locked":
			if neighbour == null:
				set_status("There's no room on that side to lead to. Add one first.")
				return
			var wanted := DMDoorEdge.Kind.LOCKED if kind == "locked" else DMDoorEdge.Kind.OPEN
			if edge:
				changes.append([edge, "kind", edge.kind, wanted])
			else:
				edge = layout.make_edge(room, neighbour)
				edge.kind = wanted
				changes.append([layout, "edges", layout.edges, with_item(layout.edges, edge)])
	commit("Change door", changes)


func shutter_rule(room: DMRoomLayout, side: int) -> DMShutterRule:
	var edge := layout.edge_on(room, side)
	return edge.shutter_for(room.id) if edge else null


func set_shutter(room: DMRoomLayout, side: int, mode: int, block_id: int) -> void:
	var rule := shutter_rule(room, side)
	if rule == null:
		return
	commit("Change shutter", [[rule, "mode", rule.mode, mode], [rule, "block_id", rule.block_id, block_id]])


# ---- Objects and warps ----

func add_placement(room: DMRoomLayout, scene: PackedScene, position: Vector2i) -> void:
	var placement := DMPlacement.new()
	placement.id = layout.next_placement_id
	placement.scene = scene
	placement.position = position
	commit("Place " + placement_label(placement), [
		[room, "placements", room.placements, with_item(room.placements, placement)],
		[layout, "next_placement_id", layout.next_placement_id, placement.id + 1],
	])
	select({ "type": "placement", "room": room.id, "id": placement.id })


func duplicate_placement(room: DMRoomLayout, placement: DMPlacement) -> void:
	var copy: DMPlacement = placement.duplicate(true)
	copy.id = layout.next_placement_id
	copy.position = (placement.position + Vector2i(16, 0)).clamp(DMNames.PLACEABLE_AREA.position, DMNames.PLACEABLE_AREA.end)
	commit("Duplicate " + placement_label(placement), [
		[room, "placements", room.placements, with_item(room.placements, copy)],
		[layout, "next_placement_id", layout.next_placement_id, copy.id + 1],
	])
	select({ "type": "placement", "room": room.id, "id": copy.id })


func remove_placement(room: DMRoomLayout, placement: DMPlacement) -> void:
	commit("Delete " + placement_label(placement), [[room, "placements", room.placements, without_item(room.placements, placement)]])


func set_override(placement: DMPlacement, key: String, value) -> void:
	var overrides := placement.overrides.duplicate()
	if value == null:
		overrides.erase(key)
	else:
		overrides[key] = value
	commit("Change " + key, [[placement, "overrides", placement.overrides, overrides]])


func set_appears(target: Object, appears: int, block_id: int) -> void:
	commit("Change when it appears", [[target, "appears", target.appears, appears], [target, "block_id", target.block_id, block_id]])


func add_warp(from_room: DMRoomLayout, from_position: Vector2i, to_room: DMRoomLayout, arrival: Vector2i) -> void:
	var warp := DMWarpLink.new()
	warp.from_room_id = from_room.id
	warp.from_position = from_position
	warp.to_room_id = to_room.id
	warp.arrival_position = arrival
	warp.warp_scene = load(DMNames.STAIRS_SCENE)
	warp.return_position = arrival + Vector2i(0, -DMNames.TILE_SIZE)
	warp.return_arrival_position = from_position + Vector2i(-DMNames.TILE_SIZE, 0)
	commit("Add warp", [[layout, "warps", layout.warps, with_item(layout.warps, warp)]])
	select({ "type": "warp", "warp": warp })


func remove_warp(warp: DMWarpLink) -> void:
	commit("Delete warp", [[layout, "warps", layout.warps, without_item(layout.warps, warp)]])


# Pushable blocks in a room, as [placement id, label] pairs for pickers.
func blocks_in(room: DMRoomLayout) -> Array:
	var blocks := []
	for placement in room.placements:
		if DMNames.is_block_scene(placement.scene):
			blocks.append([placement.id, "Block #%d at (%d, %d)" % [placement.id, placement.position.x, placement.position.y]])
	return blocks


# What a block opens or reveals: [{ label, selection }].
func block_targets(room: DMRoomLayout, block_id: int) -> Array:
	var targets := []
	for side in DMDungeonLayout.SIDE_OFFSETS:
		var rule := shutter_rule(room, side)
		if rule and rule.mode == DMShutterRule.Mode.UNTIL_BLOCK_PUSHED and rule.block_id == block_id:
			targets.append({ "label": "Opens the %s shutter" % DMDungeonLayout.side_name(side).to_lower(), "selection": { "type": "door", "room": room.id, "side": side } })
	for placement in room.placements:
		if placement.appears == DMPlacement.Appears.ON_BLOCK_PUSHED and placement.block_id == block_id:
			targets.append({ "label": "Reveals %s" % placement_label(placement), "selection": { "type": "placement", "room": room.id, "id": placement.id } })
	for warp in layout.warps:
		if warp.from_room_id == room.id and warp.appears == DMPlacement.Appears.ON_BLOCK_PUSHED and warp.block_id == block_id:
			var to := layout.get_room(warp.to_room_id)
			targets.append({ "label": "Reveals stairs to %s" % (to.room_name if to else "?"), "selection": { "type": "warp", "warp": warp } })
	return targets
