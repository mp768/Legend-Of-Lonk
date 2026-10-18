@tool
extends VBoxContainer

# Shows and edits whatever is selected: the dungeon (nothing selected), a room, a door, an object, a warp or
# the entrance point. Every edit goes through the main screen, so it's undoable. Labels sit above their fields
# so nothing is cut off, and every field explains itself in a tooltip.

const DoorSide := DMDungeonLayout.DoorSide
const Appears := DMPlacement.Appears
const DIRECTIONS := [["Any direction", Vector2.ZERO], ["Up only", Vector2.UP], ["Down only", Vector2.DOWN], ["Left only", Vector2.LEFT], ["Right only", Vector2.RIGHT]]
const APPEARS_ITEMS := ["From the start", "When the room is cleared", "When a block is pushed"]
const APPEARS_HELP := "From the start: there as soon as the room is entered.\nWhen the room is cleared: hidden until every enemy that counts toward clearing is defeated.\nWhen a block is pushed: hidden until the chosen block in this room is pushed."

# Label and tooltip for each of DMNames.CHILD_OVERRIDES. The tooltip gets the scene's own value.
const CHILD_OVERRIDE_FIELDS := {
	"LungeSpeed": ["Lunge speed", "Pixels per second it charges at Lonk. The scene's own speed is %s."],
	"ReturnSpeed": ["Return speed", "Pixels per second it slides back home after a lunge. The scene's own speed is %s."],
}

@export var main_path: NodePath
var main: Node
var _rebuild_queued := false


func _ready() -> void:
	main = get_node(main_path)
	if main == null or EditorInterface.get_edited_scene_root() == owner:
		return
	main.selection_changed.connect(_queue_rebuild)
	main.layout_changed.connect(_queue_rebuild)
	_queue_rebuild()


func _queue_rebuild() -> void:
	if not _rebuild_queued:
		_rebuild_queued = true
		_rebuild.call_deferred()


func _rebuild() -> void:
	_rebuild_queued = false
	# A text field with focus is mid-edit, so it isn't torn down under the user.
	var focused := get_viewport().gui_get_focus_owner()
	if focused and is_ancestor_of(focused) and (focused is LineEdit or focused is TextEdit):
		return
	for child in get_children():
		remove_child(child)
		child.queue_free()
	if main.layout == null:
		return
	match main.selection.get("type", ""):
		"room": _room(main.layout.get_room(main.selection.id))
		"door": _door(main.layout.get_room(main.selection.room), main.selection.side)
		"placement": _placement(main.layout.get_room(main.selection.room), main.selected_placement())
		"warp": _warp(main.selection.warp)
		"entrance": _entrance()
		_: _dungeon()


# ---- Sections ----

func _dungeon() -> void:
	var layout: DMDungeonLayout = main.layout
	_heading("Dungeon", "Node")
	_text("Name", layout.dungeon_name, "Names the dungeon scene and its files when exported.", func(v): main.set_property("Rename dungeon", layout, "dungeon_name", DMNames.node_name(v)))
	_text("Export folder", layout.export_dir, "Where Export writes <name>.tscn, <name>.dungeon.tres and Rooms/.", func(v): main.set_property("Change export folder", layout, "export_dir", v.trim_suffix("/")))
	_resource("Music", "AudioStream", layout.music, "The background track the dungeon plays.", func(v): main.set_property("Change music", layout, "music", v))
	_note("Click a room in the view to edit it. Click a + next to a room to add one. Right-click a room for more.")


func _room(room: DMRoomLayout) -> void:
	if room == null:
		return
	_heading(room.room_name, "Node2D")
	_text("Name", room.room_name, "The room's node and file name. Renaming exports to a new file; the old one is offered for deletion.", func(v): main.rename_room(room.id, v))
	if room.custom_scene:
		_note("This room uses a hand-made scene as it is. Its tiles and objects are edited in that scene; only its neighbours and warps are set up here.")
	_resource("Custom scene", "PackedScene", room.custom_scene, "Optional. Use a hand-made room scene (e.g. a side-scrolling basement) instead of building one. Leave empty to build the room here.", func(v): main.set_property("Use custom room", room, "custom_scene", v))
	_option("View", ["Top-down", "Side-scroll"], room.view, "How Lonk moves in this room. Side-scroll needs a room built for it, like the basement.", func(v): main.set_property("Change room view", room, "view", v))
	if room.custom_scene == null:
		_multiline("Text on entry", room.entry_text, "Typed out at the top of the screen every time Lonk enters, like the old man's hint. Leave empty for none.", func(v): main.set_property("Change room text", room, "entry_text", v))

	var buttons := _button_row()
	if main.focused_room_id != room.id:
		_small_button(buttons, "Edit", "Focus this room in the view.", func(): main.focus_room(room.id))
	if main.layout.start_room_id != room.id:
		_small_button(buttons, "Make start", "Lonk starts the dungeon in this room.", func(): main.set_start(room.id))
	_small_button(buttons, "Duplicate", "Copy this room, with its tiles and objects, into a free cell nearby.", func(): main.duplicate_room(room.id))

	_section("Where it is")
	if room.off_grid:
		_note("Warp-only room: off the map, reached only by stairs. It has no doors.")
		_button("Put it on the map...", "Pick an empty cell for it in the overview.", func(): main.get_node("%World").start_move(room.id))
	else:
		_note("Cell (%d, %d) on the map." % [room.grid_pos.x, room.grid_pos.y])
		var row := _button_row()
		_small_button(row, "Move...", "Pick another empty cell in the overview. Doors that no longer lead anywhere are removed after asking.", func(): main.get_node("%World").start_move(room.id))
		_small_button(row, "Make warp-only", "Take it off the map, for rooms reached only by stairs (like a basement). Its doors are removed after asking.", func(): main.move_room(room.id, room.grid_pos, true))

	if main.layout.start_room_id == room.id:
		_section("Start room")
		_vector("Lonk starts at", main.layout.entrance_point, "Where Lonk spawns, and where a Wallmaster drags him back to. You can also drag it in the room.", func(v): main.set_property("Move entrance point", main.layout, "entrance_point", v))
	_section("")
	_button("Delete room...", "Delete this room with its doors and warps. You can undo it.", func(): main.ask("Delete %s with its doors and warps? (You can undo this.)" % room.room_name, func(): main.delete_room(room.id)), true)


func _door(room: DMRoomLayout, side: int) -> void:
	if room == null:
		return
	var side_name := DMDungeonLayout.side_name(side)
	_heading("%s door of %s" % [side_name, room.room_name], "Door")
	var neighbour: DMRoomLayout = main.neighbour_on(room, side)
	var kind: String = main.door_kind(room, side)
	var start: bool = room.id == main.layout.start_room_id

	var kinds := ["wall", "open", "locked"]
	var labels := ["Wall", "Open doorway", "Locked door (needs a key)"]
	if neighbour == null:
		kinds = ["wall", "entrance"]
		labels = ["Wall", "Dungeon entrance"]
	var help := "Wall: no way through.\nOpen doorway: walk straight through.\nLocked door: costs a key, and unlocking it opens both sides."
	if neighbour:
		help += "\nThe doorway is shared with %s, so it changes on both sides." % neighbour.room_name
	else:
		help += "\nDungeon entrance: the way in from outside, on the start room. It's drawn as a doorway that can't be walked out of."
	_option("Doorway", labels, maxi(kinds.find(kind), 0), help, func(v): main.set_door(room, side, kinds[v]))
	if neighbour == null and kind == "wall":
		_note("There's no room on this side. Add one with the + in the overview to put a door here." + ("" if start else " Only the start room can have the dungeon entrance."))
	elif neighbour:
		var row := _button_row()
		_note_in(row, "Leads to %s." % neighbour.room_name)
		_small_button(row, "Go there", "Edit %s." % neighbour.room_name, func(): main.select({ "type": "door", "room": neighbour.id, "side": DMDungeonLayout.opposite(side) }))

	var rule: DMShutterRule = main.shutter_rule(room, side)
	_section("Shutter on this side")
	if rule == null:
		_note("A shutter needs a doorway. Make this an open or locked doorway first.")
		return
	_option("Shutter", ["No shutter", "Closed until the room is cleared", "Closed until a block is pushed"], rule.mode,
		"A shutter is a door on %s's side only.\nClosed until the room is cleared: shuts when Lonk comes in while enemies are left, opens when the last one that counts is defeated.\nClosed until a block is pushed: starts shut, opens when the chosen block in this room is pushed." % room.room_name,
		func(v): main.set_shutter(room, side, v, rule.block_id))
	if rule.mode == DMShutterRule.Mode.UNTIL_BLOCK_PUSHED:
		_block_picker(room, rule.block_id, "The block in %s that opens this shutter." % room.room_name, func(v): main.set_shutter(room, side, rule.mode, v))
	elif rule.mode == DMShutterRule.Mode.UNTIL_CLEARED:
		var counted := room.placements.filter(func(p): return DMNames.kind_of(p.scene) == "enemy" and p.overrides.get("CountsTowardClear", true)).size()
		_note("%d enemies in %s count toward clearing it." % [counted, room.room_name] + (" With none, the shutter never closes." if counted == 0 else ""))


func _placement(room: DMRoomLayout, placement: DMPlacement) -> void:
	if room == null or placement == null:
		return
	var kind := DMNames.kind_of(placement.scene)
	_heading(main.placement_label(placement), "PackedScene")
	if placement.scene:
		_note(placement.scene.resource_path)
	_vector("Position", placement.position, "Room pixels, exactly where it appears in the game. Drag it in the room, or nudge with the arrow keys (Shift: 8 px).", func(v): main.set_property("Move object", placement, "position", v))
	if kind == "enemy":
		_note("Enemies are always there from the start, so the room's clear tracking sees them.")
	else:
		_option("Appears", APPEARS_ITEMS, placement.appears, APPEARS_HELP, func(v): main.set_appears(placement, v, placement.block_id))
		if placement.appears == Appears.ON_BLOCK_PUSHED:
			_block_picker(room, placement.block_id, "The block in this room that reveals it.", func(v): main.set_appears(placement, placement.appears, v))

	for key in DMNames.OVERRIDES:
		if not kind in DMNames.OVERRIDES[key]:
			continue
		var current = placement.overrides.get(key)
		match key:
			"Drops":
				_resource("Drops", "Resource", current, "What it leaves behind when defeated (a DropTable), e.g. key_drop.tres. Empty keeps the enemy's own.", func(v): main.set_override(placement, key, v))
			"CountsTowardClear":
				_check("Counts toward clearing the room", current if current != null else true, "Off: the room counts as cleared without defeating this one, e.g. for traps.", func(v): main.set_override(placement, key, null if v else false))
			"AllowedDirection":
				var index := DIRECTIONS.map(func(d): return d[1]).find(current if current != null else Vector2.ZERO)
				_option("Can be pushed", DIRECTIONS.map(func(d): return d[0]), maxi(index, 0), "Which way Lonk can push it. It only moves one tile, once.", func(v): main.set_override(placement, key, null if v == 0 else DIRECTIONS[v][1]))
			"Value":
				var base: int = DMNames.root_property(placement.scene, &"Value", 1)
				_number("Amount", current if current != null else base, "How much it gives, e.g. 5 for a blue rupee.", func(v): main.set_override(placement, key, null if int(v) == base else int(v)))

	for key in DMNames.CHILD_OVERRIDES:
		var base = DMNames.child_property(placement.scene, DMNames.CHILD_OVERRIDES[key][0], DMNames.CHILD_OVERRIDES[key][1])
		if base == null:
			continue
		var current = placement.overrides.get(key)
		var label: String = CHILD_OVERRIDE_FIELDS[key][0]
		var tip: String = CHILD_OVERRIDE_FIELDS[key][1] % base
		_number(label, current if current != null else base, tip, func(v): main.set_override(placement, key, null if is_equal_approx(v, base) else float(v)))

	if kind == "block":
		_section("When it's pushed")
		var targets: Array = main.block_targets(room, placement.id)
		if targets.is_empty():
			_note("Nothing yet. Set a shutter to \"Closed until a block is pushed\", or set a pickup or stairs to appear when a block is pushed, and pick this block.")
		for target in targets:
			var row := _button_row()
			_note_in(row, target.label)
			_small_button(row, "Show", "Select it.", func(): main.select(target.selection))

	var buttons := _button_row()
	_small_button(buttons, "Duplicate", "Place a copy next to it.", func(): main.duplicate_placement(room, placement))
	_small_button(buttons, "Delete", "Remove it from the room (Delete key).", func(): main.remove_placement(room, placement), true)


func _warp(warp: DMWarpLink) -> void:
	var from: DMRoomLayout = main.layout.get_room(warp.from_room_id)
	var to: DMRoomLayout = main.layout.get_room(warp.to_room_id)
	_heading("Stairs: %s → %s" % [from.room_name if from else "?", to.room_name if to else "?"], "Warp")
	var row := _button_row()
	if from:
		_small_button(row, "Go to stairs", "Edit %s." % from.room_name, func(): main.focus_room(from.id); main.select({ "type": "warp", "warp": warp }))
	if to:
		_small_button(row, "Go to arrival", "Edit %s." % to.room_name, func(): main.focus_room(to.id); main.select({ "type": "warp", "warp": warp }))
	_resource("Stairs scene", "PackedScene", warp.warp_scene, "The scene used for the stairs (a RoomWarp), e.g. stairs.tscn.", func(v): main.set_property("Change stairs", warp, "warp_scene", v))
	_vector("Stairs at", warp.from_position, "In %s. You can drag it in the room." % (from.room_name if from else "?"), func(v): main.set_property("Move stairs", warp, "from_position", v))
	_vector("Arrives at", warp.arrival_position, "In %s, where Lonk appears. You can drag it in the room." % (to.room_name if to else "?"), func(v): main.set_property("Move arrival", warp, "arrival_position", v))
	_option("Stairs appear", APPEARS_ITEMS, warp.appears, APPEARS_HELP, func(v): main.set_appears(warp, v, warp.block_id))
	if warp.appears == Appears.ON_BLOCK_PUSHED and from:
		_block_picker(from, warp.block_id, "The block in %s that reveals the stairs." % from.room_name, func(v): main.set_appears(warp, warp.appears, v))
	_check("Way back", warp.two_way, "Also add stairs back, like the basement's ladder.", func(v): main.set_property("Change warp", warp, "two_way", v))
	if warp.two_way:
		_vector("Way back at", warp.return_position, "In %s." % (to.room_name if to else "?"), func(v): main.set_property("Move way back", warp, "return_position", v))
		_vector("Way back arrives at", warp.return_arrival_position, "In %s." % (from.room_name if from else "?"), func(v): main.set_property("Move way back arrival", warp, "return_arrival_position", v))
	_button("Delete stairs", "Remove these stairs (Delete key).", func(): main.remove_warp(warp), true)


func _entrance() -> void:
	_heading("Where Lonk starts", "CharacterBody2D")
	_note("Where Lonk spawns, and where a Wallmaster drags him back to. Drag it in the room, or type it.")
	_vector("Position", main.layout.entrance_point, "Room pixels in the start room.", func(v): main.set_property("Move entrance point", main.layout, "entrance_point", v))


# ---- Field builders ----

func _field(label_text: String, tip: String, control: Control) -> void:
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 1)
	var label := Label.new()
	label.text = label_text
	label.tooltip_text = tip
	label.mouse_filter = Control.MOUSE_FILTER_PASS
	label.modulate = Color(1, 1, 1, 0.75)
	box.add_child(label)
	control.tooltip_text = tip
	control.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	box.add_child(control)
	add_child(box)


func _heading(text: String, icon_name: String) -> void:
	var row := HBoxContainer.new()
	var icon := TextureRect.new()
	icon.texture = EditorInterface.get_base_control().get_theme_icon(icon_name, "EditorIcons")
	icon.stretch_mode = TextureRect.STRETCH_KEEP_CENTERED
	row.add_child(icon)
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_font_override("font", EditorInterface.get_base_control().get_theme_font("bold", "EditorFonts"))
	row.add_child(label)
	add_child(row)


func _section(text: String) -> void:
	add_child(HSeparator.new())
	if not text.is_empty():
		var label := Label.new()
		label.text = text
		label.add_theme_font_override("font", EditorInterface.get_base_control().get_theme_font("bold", "EditorFonts"))
		add_child(label)


func _note(text: String) -> void:
	_note_in(self, text)


func _note_in(parent: Control, text: String) -> void:
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.custom_minimum_size.x = 120
	label.modulate = Color(1, 1, 1, 0.65)
	parent.add_child(label)


func _text(label_text: String, value: String, tip: String, on_change: Callable) -> void:
	var edit := LineEdit.new()
	edit.text = value
	var submit := func():
		if edit.text != value:
			on_change.call(edit.text)
	edit.text_submitted.connect(func(_t): submit.call(); edit.release_focus())
	edit.focus_exited.connect(func(): submit.call(); _queue_rebuild())
	_field(label_text, tip, edit)


# Commits when focus leaves, so Enter can still start a new line.
func _multiline(label_text: String, value: String, tip: String, on_change: Callable) -> void:
	var edit := TextEdit.new()
	edit.text = value
	edit.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	edit.scroll_fit_content_height = true
	edit.custom_minimum_size.y = 48
	edit.focus_exited.connect(func():
		if edit.text != value:
			on_change.call(edit.text)
		_queue_rebuild())
	_field(label_text, tip, edit)


func _number(label_text: String, value: float, tip: String, on_change: Callable) -> void:
	var spin := SpinBox.new()
	spin.min_value = -9999
	spin.max_value = 9999
	spin.value = value
	spin.value_changed.connect(on_change)
	_field(label_text, tip, spin)


func _vector(label_text: String, value: Vector2i, tip: String, on_change: Callable) -> void:
	var row := HBoxContainer.new()
	var spins := []
	for axis in 2:
		var spin := SpinBox.new()
		spin.min_value = 0
		spin.max_value = DMNames.SCREEN_SIZE[axis]
		spin.value = value[axis]
		spin.prefix = "xy"[axis]
		spin.tooltip_text = tip
		spin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(spin)
		spins.append(spin)
	for spin in spins:
		spin.value_changed.connect(func(_v): on_change.call(Vector2i(int(spins[0].value), int(spins[1].value))))
	_field(label_text, tip, row)


func _option(label_text: String, items: Array, selected: int, tip: String, on_change: Callable) -> void:
	var option := OptionButton.new()
	for item in items:
		option.add_item(item)
	option.select(selected)
	option.fit_to_longest_item = false
	option.clip_text = false
	option.item_selected.connect(on_change)
	_field(label_text, tip, option)


func _check(label_text: String, value: bool, tip: String, on_change: Callable) -> void:
	var check := CheckBox.new()
	check.text = label_text
	check.button_pressed = value
	check.tooltip_text = tip
	check.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	check.toggled.connect(on_change)
	add_child(check)


func _button(text: String, tip: String, on_press: Callable, danger := false) -> void:
	var button := Button.new()
	button.text = text
	button.tooltip_text = tip
	if danger:
		button.modulate = Color(1, 0.7, 0.7)
	button.pressed.connect(on_press)
	add_child(button)


func _button_row() -> HFlowContainer:
	var row := HFlowContainer.new()
	add_child(row)
	return row


func _small_button(row: Control, text: String, tip: String, on_press: Callable, danger := false) -> void:
	var button := Button.new()
	button.text = text
	button.tooltip_text = tip
	if danger:
		button.modulate = Color(1, 0.7, 0.7)
	button.pressed.connect(on_press)
	row.add_child(button)


func _resource(label_text: String, base_type: String, value: Resource, tip: String, on_change: Callable) -> void:
	var picker := EditorResourcePicker.new()
	picker.base_type = base_type
	picker.edited_resource = value
	picker.resource_changed.connect(on_change)
	_field(label_text, tip, picker)


# A block chooser, plus a button to pick the block by clicking it in the room.
func _block_picker(room: DMRoomLayout, block_id: int, tip: String, on_change: Callable) -> void:
	var blocks: Array = main.blocks_in(room)
	var labels := ["(choose a block)"]
	var selected := 0
	for i in blocks.size():
		labels.append(blocks[i][1])
		if blocks[i][0] == block_id:
			selected = i + 1
	if block_id != -1 and selected == 0:
		labels[0] = "(block #%d was deleted)" % block_id
	_option("Opened by", labels, selected, tip, func(v): on_change.call(-1 if v == 0 else blocks[v - 1][0]))
	var row := _button_row()
	_small_button(row, "Pick in room", "Click a block in %s to choose it." % room.room_name, func():
		main.focus_room(room.id)
		main.start_pick("Click the block in %s that should trigger this." % room.room_name, "block", on_change))
	if blocks.is_empty():
		_note_in(row, "There are no blocks in this room yet. Drag a pushable block in from the Objects list.")
