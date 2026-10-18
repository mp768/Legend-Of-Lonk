@tool
extends VBoxContainer

# The whole dungeon, rendered live by the same DMRoomBuilder that exports it.
#
# Overview: click a room to focus it. Click a "+" next to a room to add one there. Click a door badge between two
# rooms to edit that door. Right-click a room for more (move, duplicate, delete...).
# Focused: the other rooms dim and the tools work on the focused room. Esc goes back to the overview.
# Zoom with the mouse wheel or a trackpad pinch. Pan with a two-finger trackpad drag, the middle mouse button,
# or Space + drag.

const Tool := preload("res://addons/dungeon_maker/ui/dm_main_screen.gd").Tool
const DoorSide := DMDungeonLayout.DoorSide
const SIDES := [DoorSide.UP, DoorSide.DOWN, DoorSide.LEFT, DoorSide.RIGHT]
const TOOLS := [
	[Tool.SELECT, "Select", "ToolSelect", KEY_V],
	[Tool.PAINT, "Paint", "Edit", KEY_B],
	[Tool.RECT, "Rectangle", "Rectangle", KEY_R],
	[Tool.ERASE, "Erase", "Eraser", KEY_E],
	[Tool.WARP, "Warp (stairs)", "Warp", KEY_W],
]
const MIN_ZOOM := 0.15
const MAX_ZOOM := 12.0
const PICK_RADIUS := 9.0
const BADGE := Vector2(52, 16)
const PLUS := 22.0

enum Menu { FOCUS, RENAME, DUPLICATE, DELETE, SET_START, MOVE, TO_WARP_ROOMS, CUSTOM, CLEAR_CUSTOM, OBJECT_DUPLICATE, OBJECT_DELETE, DOOR_WALL, DOOR_OPEN, DOOR_LOCKED }

@export var main_path: NodePath
var main: Node

var zoom := 1.0
var offset := Vector2.ZERO
# The editor's display scale (about 2 on a HiDPI screen). Overlay text, badges and lines are sized by it.
var ui := 1.0

var _area: Control
var _viewport: SubViewport
var _world: Node2D
var _view: TextureRect
var _overlay: Control
var _tool_buttons := {}
var _mode_label: Label
var _snap_check: CheckBox
var _rebuild_timer: Timer
var _previews := {}
var _builder: DMRoomBuilder
var _camera_tween: Tween

var _hover := Vector2.ZERO
var _panning := false
var _space_held := false
var _stroking := false
var _stroke_before: PackedByteArray
var _rect_start := Vector2i(-1, -1)
var _drag := {}
var _drag_before := Vector2i.ZERO
var _pending_warp := {}
# Room being moved with Move...: follows the pointer until a click drops it, Esc cancels.
var _moving_room := -1
var _menu: PopupMenu
var _menu_target := {}
var _rename_dialog: AcceptDialog
var _rename_edit: LineEdit
var _scene_dialog: EditorFileDialog
# Set when a frame was asked for before the view had a size, e.g. while the screen was hidden.
var _needs_frame := true


func _ready() -> void:
	main = get_node(main_path)
	if main == null or EditorInterface.get_edited_scene_root() == owner:
		return
	ui = EditorInterface.get_editor_scale()
	_build_toolbar()

	_area = Control.new()
	_area.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_area.clip_contents = true
	_area.focus_mode = Control.FOCUS_ALL
	_area.gui_input.connect(_on_input)
	_area.resized.connect(_on_resized)
	_area.mouse_exited.connect(func(): _hover = Vector2(-99999, -99999); _overlay.queue_redraw())
	_area.set_drag_forwarding(Callable(), _can_drop, _drop)
	add_child(_area)

	_viewport = SubViewport.new()
	_viewport.transparent_bg = false
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_viewport.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
	_viewport.disable_3d = true
	_viewport.physics_object_picking = false
	_area.add_child(_viewport)
	_world = Node2D.new()
	_viewport.add_child(_world)

	_view = TextureRect.new()
	_view.texture = _viewport.get_texture()
	_view.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_view.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_area.add_child(_view)

	_overlay = Control.new()
	_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_overlay.set_anchors_preset(Control.PRESET_FULL_RECT)
	_overlay.draw.connect(_draw_overlay)
	_area.add_child(_overlay)

	_rebuild_timer = Timer.new()
	_rebuild_timer.one_shot = true
	_rebuild_timer.wait_time = 0.05
	_rebuild_timer.timeout.connect(_rebuild_all)
	add_child(_rebuild_timer)

	_menu = PopupMenu.new()
	_menu.id_pressed.connect(_on_menu)
	add_child(_menu)
	_rename_dialog = AcceptDialog.new()
	_rename_dialog.title = "Rename room"
	_rename_edit = LineEdit.new()
	_rename_dialog.add_child(_rename_edit)
	_rename_dialog.register_text_enter(_rename_edit)
	_rename_dialog.confirmed.connect(func(): main.rename_room(_menu_target.room, _rename_edit.text))
	add_child(_rename_dialog)
	_scene_dialog = EditorFileDialog.new()
	_scene_dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_FILE
	_scene_dialog.access = EditorFileDialog.ACCESS_RESOURCES
	_scene_dialog.add_filter("*.tscn", "Room scene")
	_scene_dialog.file_selected.connect(func(path):
		var room: DMRoomLayout = main.layout.get_room(_menu_target.room)
		if room:
			main.set_property("Use custom room", room, "custom_scene", load(path)))
	add_child(_scene_dialog)

	main.layout_changed.connect(_queue_rebuild)
	main.selection_changed.connect(_overlay.queue_redraw)
	main.focus_changed.connect(_on_focus_changed)
	main.tool_changed.connect(_on_tool_changed)
	main.frame_requested.connect(func(): _frame(main.focused_room_id, true))
	_on_tool_changed()


func _build_toolbar() -> void:
	var bar := HBoxContainer.new()
	add_child(bar)
	var overview := Button.new()
	overview.text = "Overview"
	overview.flat = true
	overview.icon = _editor_icon("ArrowUp")
	overview.tooltip_text = "Back to the whole dungeon (Esc)."
	overview.pressed.connect(func(): main.show_overview())
	bar.add_child(overview)
	bar.add_child(VSeparator.new())
	var group := ButtonGroup.new()
	for entry in TOOLS:
		var button := Button.new()
		button.toggle_mode = true
		button.button_group = group
		button.flat = true
		button.icon = _editor_icon(entry[2])
		button.tooltip_text = "%s (%s)\n%s" % [entry[1], OS.get_keycode_string(entry[3]), main.TOOL_HELP[entry[0]]]
		button.pressed.connect(func(): main.set_tool(entry[0]))
		bar.add_child(button)
		_tool_buttons[entry[0]] = button
	bar.add_child(VSeparator.new())
	_snap_check = CheckBox.new()
	_snap_check.text = "Snap pickups"
	_snap_check.tooltip_text = "Snap collectibles to an 8 px grid. Enemies snap to 8 px and blocks and stairs to tile centres either way. Hold Alt to place anything freely."
	_snap_check.toggled.connect(func(on): main.snap_collectibles = on)
	bar.add_child(_snap_check)
	var fit := Button.new()
	fit.flat = true
	fit.icon = _editor_icon("ZoomReset")
	fit.tooltip_text = "Fit the view to the focused room, or to the whole dungeon (F)."
	fit.pressed.connect(func(): _frame(main.focused_room_id, true))
	bar.add_child(fit)
	_mode_label = Label.new()
	_mode_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_mode_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_mode_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_mode_label.mouse_filter = Control.MOUSE_FILTER_PASS
	bar.add_child(_mode_label)


func _editor_icon(icon_name: String) -> Texture2D:
	return EditorInterface.get_base_control().get_theme_icon(icon_name, "EditorIcons")


func _exit_tree() -> void:
	if _camera_tween:
		_camera_tween.kill()


# ---- Camera ----

func _on_resized() -> void:
	_viewport.size = Vector2i(_area.size.max(Vector2.ONE))
	_view.size = _area.size
	if _needs_frame and _area.size.x > 10:
		_frame(main.focused_room_id, false)
	_apply_camera()


func _apply_camera() -> void:
	offset = offset.round()
	_viewport.canvas_transform = Transform2D(0, Vector2(zoom, zoom), 0, offset)
	_overlay.queue_redraw()


func _set_camera(new_zoom: float, new_offset: Vector2) -> void:
	zoom = clampf(new_zoom, MIN_ZOOM, MAX_ZOOM)
	offset = new_offset
	_apply_camera()


func zoom_at(screen_point: Vector2, factor: float) -> void:
	var world_point := to_world(screen_point)
	var new_zoom := clampf(zoom * factor, MIN_ZOOM, MAX_ZOOM)
	_set_camera(new_zoom, screen_point - world_point * new_zoom)


func to_world(screen_point: Vector2) -> Vector2:
	return (screen_point - offset) / zoom


func to_screen(world_point: Vector2) -> Vector2:
	return world_point * zoom + offset


func screen_rect(world_rect: Rect2) -> Rect2:
	return Rect2(to_screen(world_rect.position), world_rect.size * zoom)


# Frames a room (or the whole dungeon for -1), with a short glide.
func _frame(room_id: int, animate: bool) -> void:
	if _area.size.x < 10 or main.layout == null:
		_needs_frame = true
		return
	_needs_frame = false
	var target: Rect2 = dungeon_rect() if room_id == -1 else room_world_rect(main.layout.get_room(room_id))
	target = target.grow(24 if room_id != -1 else 48)
	var fit := (_area.size / target.size)
	var new_zoom := clampf(minf(fit.x, fit.y), MIN_ZOOM, MAX_ZOOM)
	if room_id != -1 and new_zoom > 1.0:
		new_zoom = floorf(new_zoom * 2.0) / 2.0
	var new_offset := _area.size / 2 - target.get_center() * new_zoom
	if _camera_tween:
		_camera_tween.kill()
	if not animate or not is_visible_in_tree():
		_set_camera(new_zoom, new_offset)
		return
	var from_zoom := zoom
	var from_offset := offset
	_camera_tween = create_tween()
	_camera_tween.tween_method(func(t: float):
		_set_camera(lerpf(from_zoom, new_zoom, t), from_offset.lerp(new_offset, t)), 0.0, 1.0, 0.18).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)


func _on_focus_changed() -> void:
	_pending_warp = {}
	_moving_room = -1
	_update_dimming()
	_frame(main.focused_room_id, true)
	_on_tool_changed()


func _on_tool_changed() -> void:
	if _tool_buttons.has(main.tool):
		_tool_buttons[main.tool].button_pressed = true
	var room: DMRoomLayout = main.focused_room()
	var text := "Overview (click a room)" if room == null else "Editing %s (Esc: overview)" % room.room_name
	if main.tool == Tool.PLACE and main.object_scene:
		text = "Placing %s: click to place, Shift keeps placing, Esc stops" % main.object_scene.resource_path.get_file().get_basename()
	if not main.pick.is_empty():
		text = main.pick.prompt + " (Esc cancels)"
	if _moving_room != -1:
		text = "Click an empty cell to move the room there (Esc cancels)"
	_mode_label.text = text
	_mode_label.tooltip_text = text + "\n" + main.TOOL_HELP[main.tool]
	_overlay.queue_redraw()


# ---- Rendering the rooms ----

func _queue_rebuild() -> void:
	_rebuild_timer.start()


func _rebuild_all() -> void:
	for room_id in _previews:
		if is_instance_valid(_previews[room_id]):
			_previews[room_id].queue_free()
	_previews.clear()
	if main.layout == null or main.stamps == null:
		return
	_builder = DMRoomBuilder.new(main.layout, main.stamps, true)
	for room in main.layout.rooms:
		_build_room(room)
	_update_dimming()
	_overlay.queue_redraw()


func _build_room(room: DMRoomLayout) -> void:
	if _previews.has(room.id) and is_instance_valid(_previews[room.id]):
		_previews[room.id].queue_free()
	var preview: Node2D = room.custom_scene.instantiate() if room.custom_scene else _builder.build(room)
	preview.position = Vector2(DMDungeonBuilder.room_position(main.layout, room) * DMNames.SCREEN_SIZE)
	_world.add_child(preview)
	_previews[room.id] = preview


# Rebuilds one room straight away, for live feedback while painting or dragging.
func rebuild_room(room: DMRoomLayout) -> void:
	if _builder == null:
		_rebuild_all()
		return
	_build_room(room)
	_update_dimming()


func _update_dimming() -> void:
	for room_id in _previews:
		var focused: bool = main.focused_room_id == -1 or main.focused_room_id == room_id
		_previews[room_id].modulate = Color.WHITE if focused else Color(0.45, 0.45, 0.5)


func room_world_rect(room: DMRoomLayout) -> Rect2:
	if room == null:
		return Rect2(Vector2.ZERO, DMNames.SCREEN_SIZE)
	return Rect2(Vector2(DMDungeonBuilder.room_position(main.layout, room) * DMNames.SCREEN_SIZE), DMNames.SCREEN_SIZE)


func cell_world_rect(cell: Vector2i) -> Rect2:
	return Rect2(Vector2(cell * DMNames.SCREEN_SIZE), DMNames.SCREEN_SIZE)


func dungeon_rect() -> Rect2:
	var rect := Rect2()
	var first := true
	for room in main.layout.rooms:
		var r := room_world_rect(room)
		rect = r if first else rect.merge(r)
		first = false
	return rect if not first else cell_world_rect(Vector2i.ZERO)


func room_at(world_point: Vector2) -> DMRoomLayout:
	for room in main.layout.rooms:
		if room_world_rect(room).has_point(world_point):
			return room
	return null


func grid_cell_at(world_point: Vector2) -> Vector2i:
	return Vector2i((world_point / Vector2(DMNames.SCREEN_SIZE)).floor())


# Empty cells next to a room on the grid, where a "+" adds a new room.
func plus_cells() -> Array:
	var cells := {}
	for room in main.layout.rooms:
		if room.off_grid:
			continue
		for side in SIDES:
			var cell: Vector2i = room.grid_pos + DMDungeonLayout.SIDE_OFFSETS[side]
			if main.layout.room_at(cell) == null and not _warp_row_cell(cell):
				cells[cell] = true
	if main.layout.rooms.filter(func(r): return not r.off_grid).is_empty():
		cells[Vector2i.ZERO] = true
	return cells.keys()


func _warp_row_cell(cell: Vector2i) -> bool:
	for room in main.layout.rooms:
		if room.off_grid and DMDungeonBuilder.room_position(main.layout, room) == cell:
			return true
	return false


func plus_rect(cell: Vector2i) -> Rect2:
	var size_px := Vector2(PLUS, PLUS) * ui
	return Rect2(screen_rect(cell_world_rect(cell)).get_center() - size_px / 2, size_px)


# ---- Doors ----

# Screen rect of the badge on one side of a room.
func door_badge(room: DMRoomLayout, side: int) -> Rect2:
	var rect := screen_rect(room_world_rect(room))
	var centre := rect.get_center() + Vector2(DMDungeonLayout.SIDE_OFFSETS[side]) * rect.size / 2
	# Inside the room when focused, so it sits on the wall; on the boundary in the overview.
	if main.focused_room_id == room.id:
		centre -= Vector2(DMDungeonLayout.SIDE_OFFSETS[side]) * Vector2(20, 20) * zoom
	var size_px := BADGE * ui
	if main.focused_room_id == room.id:
		size_px.x *= 1.6
	return Rect2(centre - size_px / 2, size_px)


# Door badges that can be clicked: every side of the focused room, and every doorway in the overview.
func door_badges() -> Array:
	var badges := []
	var focused: DMRoomLayout = main.focused_room()
	if focused:
		if not focused.off_grid:
			for side in SIDES:
				badges.append({ "room": focused, "side": side, "rect": door_badge(focused, side) })
		return badges
	for room in main.layout.rooms:
		for side in [DoorSide.RIGHT, DoorSide.DOWN]:
			if main.neighbour_on(room, side):
				badges.append({ "room": room, "side": side, "rect": door_badge(room, side) })
		if room.id == main.layout.start_room_id and main.layout.entrance_side != DoorSide.NONE and not room.off_grid:
			badges.append({ "room": room, "side": main.layout.entrance_side, "rect": door_badge(room, main.layout.entrance_side) })
	return badges


func badge_at(screen_point: Vector2) -> Dictionary:
	for badge in door_badges():
		if badge.rect.grow(2 * ui).has_point(screen_point):
			return badge
	return {}


func door_text(room: DMRoomLayout, side: int) -> String:
	var kind: String = main.door_kind(room, side)
	var text: String = { "wall": "Wall", "open": "Open", "locked": "Locked", "entrance": "Entrance" }[kind]
	var rule: DMShutterRule = main.shutter_rule(room, side)
	if main.focused_room_id == room.id and rule and rule.mode != DMShutterRule.Mode.NONE:
		text += "+Shutter"
	return text


# ---- Picking things in the focused room ----

func room_pixel(room: DMRoomLayout, screen_point: Vector2) -> Vector2:
	return to_world(screen_point) - room_world_rect(room).position


func tile_at(pixel: Vector2) -> Vector2i:
	return Vector2i(((pixel - Vector2(DMNames.INTERIOR_ORIGIN * DMNames.CELL_SIZE)) / DMNames.TILE_SIZE).floor())


# Snaps by kind: enemies to 8 px, blocks and stairs to tile centres, collectibles only when Snap is on.
func snap(pixel: Vector2, scene: PackedScene, free: bool) -> Vector2i:
	var point := Vector2i(pixel.round())
	if not free:
		match DMNames.kind_of(scene):
			"enemy":
				point = Vector2i((pixel / 8).round() * 8)
			"block", "warp":
				point = Vector2i(((pixel - Vector2(8, 8)) / 16).round() * 16 + Vector2(8, 8))
			_:
				if main.snap_collectibles:
					point = Vector2i((pixel / 8).round() * 8)
	return point.clamp(DMNames.PLACEABLE_AREA.position, DMNames.PLACEABLE_AREA.end)


# Something to select or drag under the pointer: an object, a warp end, or the entrance point.
func pick_at(room: DMRoomLayout, pixel: Vector2) -> Dictionary:
	var best := {}
	var best_distance := PICK_RADIUS + 4.0 * ui / zoom
	for placement in room.placements:
		var distance := pixel.distance_to(Vector2(placement.position))
		if distance < best_distance:
			best_distance = distance
			best = { "selection": { "type": "placement", "room": room.id, "id": placement.id }, "object": placement, "property": "position", "scene": placement.scene, "placement": placement }
	for point in warp_points(room):
		var distance := pixel.distance_to(Vector2(point.warp.get(point.property)))
		if distance < best_distance:
			best_distance = distance
			best = { "selection": { "type": "warp", "warp": point.warp }, "object": point.warp, "property": point.property, "scene": load(DMNames.STAIRS_SCENE) }
	if room.id == main.layout.start_room_id and pixel.distance_to(Vector2(main.layout.entrance_point)) < best_distance:
		best = { "selection": { "type": "entrance" }, "object": main.layout, "property": "entrance_point", "scene": null }
	return best


# Warp ends that lie in this room: [{ warp, property, label }].
func warp_points(room: DMRoomLayout) -> Array:
	var points := []
	for warp in main.layout.warps:
		if warp.from_room_id == room.id:
			points.append({ "warp": warp, "property": "from_position", "label": "Stairs" })
			if warp.two_way:
				points.append({ "warp": warp, "property": "return_arrival_position", "label": "Back arrives" })
		if warp.to_room_id == room.id:
			points.append({ "warp": warp, "property": "arrival_position", "label": "Arrives" })
			if warp.two_way:
				points.append({ "warp": warp, "property": "return_position", "label": "Way back" })
	return points


# ---- Input ----

func _unhandled_key_input(event: InputEvent) -> void:
	if not is_visible_in_tree() or main == null or main.layout == null:
		return
	var focus := get_viewport().gui_get_focus_owner()
	if focus is LineEdit or focus is TextEdit:
		return
	if focus and not main.is_ancestor_of(focus):
		return
	if event.keycode == KEY_SPACE:
		_space_held = event.pressed
		return
	if not event.pressed or (event.echo and event.keycode not in [KEY_LEFT, KEY_RIGHT, KEY_UP, KEY_DOWN]):
		return
	if _handle_key(event):
		get_viewport().set_input_as_handled()


func _handle_key(event: InputEventKey) -> bool:
	if event.ctrl_pressed or event.meta_pressed:
		return false
	match event.keycode:
		KEY_ESCAPE:
			_escape()
			return true
		KEY_F:
			_frame(main.focused_room_id, true)
			return true
		KEY_DELETE, KEY_BACKSPACE:
			var placement: DMPlacement = main.selected_placement()
			if placement:
				main.remove_placement(main.layout.get_room(main.selection.room), placement)
			elif main.selection.get("type") == "warp":
				main.remove_warp(main.selection.warp)
			return true
	var nudge := { KEY_LEFT: Vector2i.LEFT, KEY_RIGHT: Vector2i.RIGHT, KEY_UP: Vector2i.UP, KEY_DOWN: Vector2i.DOWN }
	if nudge.has(event.keycode):
		var placement: DMPlacement = main.selected_placement()
		if placement:
			var step: Vector2i = nudge[event.keycode] * (8 if event.shift_pressed else 1)
			main.set_property("Nudge object", placement, "position", (placement.position + step).clamp(DMNames.PLACEABLE_AREA.position, DMNames.PLACEABLE_AREA.end))
			return true
		return false
	if main.focused_room_id != -1:
		for entry in TOOLS:
			if event.keycode == entry[3]:
				main.set_tool(entry[0])
				return true
	return false


# Esc backs out one step: a pending pick, move, warp or Place, then the focused room.
func _escape() -> void:
	if not main.pick.is_empty():
		main.pick = {}
		main.set_status("Cancelled.")
		main.tool_changed.emit()
	elif _moving_room != -1:
		_moving_room = -1
		_on_tool_changed()
	elif not _pending_warp.is_empty():
		_pending_warp = {}
		_overlay.queue_redraw()
	elif main.tool != Tool.SELECT:
		main.set_tool(Tool.SELECT)
	elif main.focused_room_id != -1:
		main.show_overview()


func _on_input(event: InputEvent) -> void:
	if main.layout == null:
		return
	if event is InputEventMagnifyGesture:
		zoom_at(event.position, event.factor)
	elif event is InputEventPanGesture:
		_set_camera(zoom, offset - event.delta * 20.0)
	elif event is InputEventMouseButton:
		_area.grab_focus()
		if event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
			var factor := 1.1 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.0 / 1.1
			zoom_at(event.position, pow(factor, maxf(event.factor, 1.0)) if event.factor > 0 else factor)
		elif event.button_index == MOUSE_BUTTON_MIDDLE or (event.button_index == MOUSE_BUTTON_LEFT and _space_held):
			_panning = event.pressed
		elif event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_press(event.position, event.shift_pressed, event.alt_pressed)
			else:
				_release(event.position)
		elif event.button_index == MOUSE_BUTTON_RIGHT and event.pressed:
			_context_menu(event.position)
		_area.accept_event()
	elif event is InputEventMouseMotion:
		_hover = event.position
		if _panning:
			_set_camera(zoom, offset + event.relative)
		else:
			_motion(event.position, event.alt_pressed)
		_overlay.queue_redraw()


func _press(point: Vector2, shift: bool, alt: bool) -> void:
	var world_point := to_world(point)
	var focused: DMRoomLayout = main.focused_room()
	if _moving_room != -1:
		_drop_moving_room(grid_cell_at(world_point))
		return
	if focused == null:
		_press_overview(point, world_point)
		return
	var in_room := room_world_rect(focused).has_point(world_point)
	var pixel := room_pixel(focused, point)

	if not main.pick.is_empty():
		var target := pick_at(focused, pixel) if in_room else {}
		var placement: DMPlacement = target.get("placement")
		if placement and DMNames.kind_of(placement.scene) == main.pick.kind:
			main.finish_pick(placement.id)
		else:
			main.set_status("That isn't a %s. %s" % [main.pick.kind, main.pick.prompt])
		return

	if main.tool == Tool.WARP and not _pending_warp.is_empty() and not in_room:
		var other := room_at(world_point)
		if other:
			main.focus_room(other.id)
		return
	if not in_room:
		var other := room_at(world_point)
		if other:
			main.focus_room(other.id)
		return

	match main.tool:
		Tool.SELECT:
			var badge := badge_at(point)
			if not badge.is_empty():
				main.select({ "type": "door", "room": badge.room.id, "side": badge.side })
				return
			_drag = pick_at(focused, pixel)
			if _drag.is_empty():
				main.select({ "type": "room", "id": focused.id })
			else:
				main.select(_drag.selection)
				_drag_before = _drag.object.get(_drag.property)
		Tool.PAINT, Tool.ERASE:
			if _editable(focused):
				_stroke_before = focused.tiles
				_stroking = true
				_paint(focused, tile_at(pixel))
		Tool.RECT:
			if _editable(focused) and DMRoomLayout.in_bounds(tile_at(pixel)):
				_rect_start = tile_at(pixel)
		Tool.PLACE:
			if main.object_scene and _editable(focused):
				main.add_placement(focused, main.object_scene, snap(pixel, main.object_scene, alt))
				if not shift:
					main.set_tool(Tool.SELECT)
		Tool.WARP:
			_warp_click(focused, snap(pixel, load(DMNames.STAIRS_SCENE), alt))


func _press_overview(point: Vector2, world_point: Vector2) -> void:
	for cell in plus_cells():
		if plus_rect(cell).has_point(point):
			main.add_room(cell)
			return
	var badge := badge_at(point)
	if not badge.is_empty():
		main.select({ "type": "door", "room": badge.room.id, "side": badge.side })
		return
	var room := room_at(world_point)
	if room:
		main.focus_room(room.id)
	else:
		main.select({})


func _motion(point: Vector2, alt: bool) -> void:
	var focused: DMRoomLayout = main.focused_room()
	if focused == null:
		return
	var pixel := room_pixel(focused, point)
	if _stroking:
		_paint(focused, tile_at(pixel))
	elif not _drag.is_empty():
		_drag.object.set(_drag.property, snap(pixel, _drag.get("scene"), alt))
		rebuild_room(focused)


func _release(point: Vector2) -> void:
	var focused: DMRoomLayout = main.focused_room()
	if _stroking:
		_stroking = false
		var after := focused.tiles
		focused.tiles = _stroke_before
		if after != _stroke_before:
			main.commit("Paint tiles", [[focused, "tiles", _stroke_before, after]])
	elif _rect_start != Vector2i(-1, -1):
		var end := tile_at(room_pixel(focused, point)).clamp(Vector2i.ZERO, Vector2i(DMRoomLayout.TILES_WIDE - 1, DMRoomLayout.TILES_HIGH - 1))
		var rect := Rect2i(_rect_start.min(end), (end - _rect_start).abs() + Vector2i.ONE)
		var tiles := focused.tiles.duplicate()
		for y in range(rect.position.y, rect.end.y):
			for x in range(rect.position.x, rect.end.x):
				tiles[y * DMRoomLayout.TILES_WIDE + x] = brush_for_tool()
		_rect_start = Vector2i(-1, -1)
		main.commit("Fill rectangle", [[focused, "tiles", focused.tiles, tiles]])
	elif not _drag.is_empty():
		var object: Object = _drag.object
		var property: String = _drag.property
		var after: Vector2i = object.get(property)
		object.set(property, _drag_before)
		_drag = {}
		if after != _drag_before:
			main.commit("Move", [[object, property, _drag_before, after]])


func brush_for_tool() -> int:
	return 0 if main.tool == Tool.ERASE else main.brush_index


func _editable(room: DMRoomLayout) -> bool:
	if room.custom_scene:
		main.set_status("%s uses a custom scene as it is, so its tiles and objects are edited in that scene. Warps still work here." % room.room_name)
		return false
	return true


func _paint(room: DMRoomLayout, tile: Vector2i) -> void:
	if not DMRoomLayout.in_bounds(tile):
		return
	var index := tile.y * DMRoomLayout.TILES_WIDE + tile.x
	if room.tiles[index] == brush_for_tool():
		return
	var tiles := room.tiles.duplicate()
	tiles[index] = brush_for_tool()
	room.tiles = tiles
	rebuild_room(room)


# First click places the stairs. The arrival is then clicked in another room (click a room to go there).
func _warp_click(room: DMRoomLayout, position: Vector2i) -> void:
	if _pending_warp.is_empty():
		_pending_warp = { "room": room.id, "position": position }
		main.set_status("Stairs placed. Now click another room, then click where they arrive.")
	elif _pending_warp.room == room.id:
		_pending_warp.position = position
		main.set_status("Stairs moved. Now click another room, then click where they arrive.")
	else:
		main.add_warp(main.layout.get_room(_pending_warp.room), _pending_warp.position, room, position)
		_pending_warp = {}
		main.set_tool(Tool.SELECT)
		main.set_status("Warp added.")
	_overlay.queue_redraw()


func _drop_moving_room(cell: Vector2i) -> void:
	var room: DMRoomLayout = main.layout.get_room(_moving_room)
	if room == null or (main.layout.room_at(cell) not in [null, room]) or _warp_row_cell(cell):
		main.set_status("That cell is taken. Pick an empty one, or Esc to cancel.")
		return
	_moving_room = -1
	_on_tool_changed()
	main.move_room(room.id, cell, false)


# ---- Context menu ----

func _context_menu(point: Vector2) -> void:
	var world_point := to_world(point)
	var focused: DMRoomLayout = main.focused_room()
	_menu.clear()
	var badge := badge_at(point)
	if not badge.is_empty() and main.neighbour_on(badge.room, badge.side):
		_menu_target = { "room": badge.room.id, "side": badge.side }
		_menu.add_separator("%s door of %s" % [DMDungeonLayout.side_name(badge.side), badge.room.room_name])
		_menu.add_item("Wall", Menu.DOOR_WALL)
		_menu.add_item("Open doorway", Menu.DOOR_OPEN)
		_menu.add_item("Locked door", Menu.DOOR_LOCKED)
	elif focused and room_world_rect(focused).has_point(world_point) and pick_at(focused, room_pixel(focused, point)).get("placement") != null:
		var placement: DMPlacement = pick_at(focused, room_pixel(focused, point)).placement
		main.select({ "type": "placement", "room": focused.id, "id": placement.id })
		_menu_target = { "room": focused.id, "placement": placement }
		_menu.add_separator(main.placement_label(placement))
		_menu.add_item("Duplicate", Menu.OBJECT_DUPLICATE)
		_menu.add_item("Delete", Menu.OBJECT_DELETE)
	else:
		var room := room_at(world_point)
		if room == null:
			return
		_menu_target = { "room": room.id }
		_menu.add_separator(room.room_name)
		if main.focused_room_id != room.id:
			_menu.add_item("Edit this room", Menu.FOCUS)
		_menu.add_item("Rename...", Menu.RENAME)
		_menu.add_item("Set as start room", Menu.SET_START)
		_menu.set_item_disabled(_menu.get_item_index(Menu.SET_START), main.layout.start_room_id == room.id)
		_menu.add_item("Duplicate", Menu.DUPLICATE)
		_menu.add_separator()
		_menu.add_item("Move to another cell...", Menu.MOVE)
		if not room.off_grid:
			_menu.add_item("Make warp-only (off the grid)", Menu.TO_WARP_ROOMS)
			_menu.set_item_tooltip(_menu.get_item_index(Menu.TO_WARP_ROOMS), "For rooms reached only by stairs, like a basement. They sit in a row below the map and have no doors.")
		_menu.add_item("Use a custom scene...", Menu.CUSTOM)
		_menu.set_item_tooltip(_menu.get_item_index(Menu.CUSTOM), "Use a hand-made room scene as it is (e.g. a side-scrolling basement). Only its neighbours and warps are set up.")
		if room.custom_scene:
			_menu.add_item("Stop using the custom scene", Menu.CLEAR_CUSTOM)
		_menu.add_separator()
		_menu.add_item("Delete room...", Menu.DELETE)
	_menu.popup(Rect2i(Vector2i(_area.get_screen_position() + point), Vector2i.ZERO))


func _on_menu(id: int) -> void:
	var room: DMRoomLayout = main.layout.get_room(_menu_target.get("room", -1))
	if room == null:
		return
	match id:
		Menu.FOCUS: main.focus_room(room.id)
		Menu.RENAME:
			_rename_edit.text = room.room_name
			_rename_dialog.popup_centered(Vector2i(300, 80))
			_rename_edit.grab_focus()
			_rename_edit.select_all()
		Menu.SET_START: main.set_start(room.id)
		Menu.DUPLICATE: main.duplicate_room(room.id)
		Menu.MOVE: start_move(room.id)
		Menu.TO_WARP_ROOMS: main.move_room(room.id, room.grid_pos, true)
		Menu.CUSTOM: _scene_dialog.popup_file_dialog()
		Menu.CLEAR_CUSTOM: main.set_property("Stop using custom room", room, "custom_scene", null)
		Menu.DELETE: main.ask("Delete %s with its doors and warps? (You can undo this.)" % room.room_name, func(): main.delete_room(room.id))
		Menu.OBJECT_DUPLICATE: main.duplicate_placement(room, _menu_target.placement)
		Menu.OBJECT_DELETE: main.remove_placement(room, _menu_target.placement)
		Menu.DOOR_WALL: main.set_door(room, _menu_target.side, "wall")
		Menu.DOOR_OPEN: main.set_door(room, _menu_target.side, "open")
		Menu.DOOR_LOCKED: main.set_door(room, _menu_target.side, "locked")


# Moving is its own mode, so a stray click or drag never moves a room.
func start_move(room_id: int) -> void:
	if main.focused_room_id != -1:
		main.show_overview()
	_moving_room = room_id
	_on_tool_changed()


# ---- Drag and drop from the palette ----

func _can_drop(point: Vector2, data) -> bool:
	if not (data is Dictionary and data.has("dm_scene")):
		return false
	_hover = point
	_overlay.queue_redraw()
	var room := room_at(to_world(point))
	return room != null and room.custom_scene == null


func _drop(point: Vector2, data) -> void:
	var room := room_at(to_world(point))
	if room == null:
		return
	var scene: PackedScene = load(data.dm_scene)
	main.focus_room(room.id)
	main.add_placement(room, scene, snap(room_pixel(room, point), scene, Input.is_key_pressed(KEY_ALT)))
	main.set_tool(Tool.SELECT)
	_area.grab_focus()


# ---- Overlay ----

func _draw_overlay() -> void:
	if main == null or main.layout == null:
		return
	var font := get_theme_default_font()
	var accent := EditorInterface.get_base_control().get_theme_color("accent_color", "Editor")
	var focused: DMRoomLayout = main.focused_room()
	var hover_world := to_world(_hover)

	if focused == null:
		_draw_overview(font, accent, hover_world)
	else:
		_draw_focused(focused, font, accent)
	for badge in door_badges():
		_draw_badge(badge, font, accent)
	_draw_warp_lines(focused)
	if _moving_room != -1:
		var cell := grid_cell_at(hover_world)
		var ok: bool = main.layout.room_at(cell) in [null, main.layout.get_room(_moving_room)] and not _warp_row_cell(cell)
		_overlay.draw_rect(screen_rect(cell_world_rect(cell)), (Color(0.3, 1, 0.4) if ok else Color.RED), false, 3.0 * ui)


func _draw_overview(font: Font, accent: Color, hover_world: Vector2) -> void:
	var warp_rooms: Array = main.layout.rooms.filter(func(r): return r.off_grid)
	if not warp_rooms.is_empty():
		var first := screen_rect(room_world_rect(warp_rooms[0]))
		_text(font, first.position + Vector2(0, -8 * ui), "Warp-only rooms (reached by stairs)", Color(1, 1, 1, 0.7), 13)
	for room in main.layout.rooms:
		var rect := screen_rect(room_world_rect(room))
		var hovered := room_world_rect(room).has_point(hover_world) and _moving_room == -1
		var selected: bool = main.selection.get("type") == "room" and main.selection.id == room.id
		if hovered or selected:
			_overlay.draw_rect(rect, accent, false, (3.0 if selected else 2.0) * ui)
		var title: String = room.room_name + ("  (custom)" if room.custom_scene else "")
		if room.id == main.layout.start_room_id:
			title = "★ " + title
		# Inside the room's corner on a dark tag, clear of the door badges between rooms.
		var title_size := font.get_string_size(title, HORIZONTAL_ALIGNMENT_LEFT, -1, int(13 * ui))
		var tag := Rect2(rect.position + Vector2(4, 4) * ui, title_size + Vector2(8, 2) * ui)
		_overlay.draw_rect(tag, Color(0, 0, 0, 0.6))
		_text(font, tag.position + Vector2(4 * ui, title_size.y * 0.8), title, Color.GOLD if room.id == main.layout.start_room_id else Color.WHITE, 13)
	if _moving_room == -1:
		for cell in plus_cells():
			var rect := plus_rect(cell)
			var hovered := rect.grow(4).has_point(_hover)
			_overlay.draw_rect(screen_rect(cell_world_rect(cell)).grow(-4), Color(1, 1, 1, 0.06 if not hovered else 0.12))
			_overlay.draw_rect(rect, Color(0.3, 0.8, 0.4, 0.9 if hovered else 0.5))
			_overlay.draw_string(font, rect.position + Vector2(0, rect.size.y * 0.8), "+", HORIZONTAL_ALIGNMENT_CENTER, rect.size.x, int(20 * ui), Color.WHITE)
			if hovered:
				_text(font, rect.position + Vector2(-20, 40) * ui, "Add a room here", Color.WHITE, 12)


func _draw_focused(room: DMRoomLayout, font: Font, accent: Color) -> void:
	var origin := room_world_rect(room).position
	var p := func(pixel: Vector2) -> Vector2: return to_screen(origin + pixel)
	_overlay.draw_rect(screen_rect(room_world_rect(room)), accent, false, 2.0 * ui)
	if not room.custom_scene and main.tool in [Tool.PAINT, Tool.RECT, Tool.ERASE]:
		var interior_origin := Vector2(DMNames.INTERIOR_ORIGIN * DMNames.CELL_SIZE)
		for x in DMRoomLayout.TILES_WIDE + 1:
			var a: Vector2 = p.call(interior_origin + Vector2(x * DMNames.TILE_SIZE, 0))
			_overlay.draw_line(a, a + Vector2(0, DMRoomLayout.TILES_HIGH * DMNames.TILE_SIZE * zoom), Color(1, 1, 1, 0.15))
		for y in DMRoomLayout.TILES_HIGH + 1:
			var a: Vector2 = p.call(interior_origin + Vector2(0, y * DMNames.TILE_SIZE))
			_overlay.draw_line(a, a + Vector2(DMRoomLayout.TILES_WIDE * DMNames.TILE_SIZE * zoom, 0), Color(1, 1, 1, 0.15))
		var hover_tile := tile_at(room_pixel(room, _hover))
		if DMRoomLayout.in_bounds(hover_tile):
			var from := hover_tile if _rect_start == Vector2i(-1, -1) else _rect_start.min(hover_tile)
			var to := hover_tile if _rect_start == Vector2i(-1, -1) else _rect_start.max(hover_tile)
			var a: Vector2 = p.call(interior_origin + Vector2(from * DMNames.TILE_SIZE))
			var b: Vector2 = p.call(interior_origin + Vector2((to + Vector2i.ONE) * DMNames.TILE_SIZE))
			_overlay.draw_rect(Rect2(a, b - a), accent, false, 2.0 * ui)

	# Links from each block to whatever it opens or reveals.
	for placement in room.placements:
		if DMNames.is_block_scene(placement.scene):
			for target in main.block_targets(room, placement.id):
				var to := _target_point(room, target.selection)
				if to != Vector2.INF:
					_overlay.draw_dashed_line(p.call(Vector2(placement.position)), to, Color.ORANGE, 2.0 * ui, 6.0 * ui)

	var selected: DMPlacement = main.selected_placement()
	for placement in room.placements:
		var at: Vector2 = p.call(Vector2(placement.position))
		var hovered := Vector2(placement.position).distance_to(room_pixel(room, _hover)) < PICK_RADIUS + 4.0 * ui / zoom
		if placement == selected:
			_overlay.draw_rect(Rect2(at - Vector2(9, 9) * zoom, Vector2(18, 18) * zoom), accent, false, 2.0 * ui)
		elif hovered and main.tool == Tool.SELECT:
			_overlay.draw_rect(Rect2(at - Vector2(9, 9) * zoom, Vector2(18, 18) * zoom), Color(1, 1, 1, 0.6), false, 1.0 * ui)
		if placement.appears != DMPlacement.Appears.IMMEDIATELY and DMNames.kind_of(placement.scene) != "enemy":
			_text(font, at + Vector2(-20 * ui, 10 * zoom + 12 * ui), "on clear" if placement.appears == DMPlacement.Appears.ON_ROOM_CLEARED else "on push", Color(0.8, 0.9, 1.0), 11)
		if not main.pick.is_empty() and DMNames.kind_of(placement.scene) == main.pick.kind:
			_overlay.draw_circle(at, 10 * zoom, Color(1, 0.6, 0.1, 0.35))

	for point in warp_points(room):
		var at: Vector2 = p.call(Vector2(point.warp.get(point.property)))
		var is_selected: bool = main.selection.get("warp") == point.warp
		_overlay.draw_rect(Rect2(at - Vector2(8, 8) * zoom, Vector2(16, 16) * zoom), accent if is_selected else Color(0.85, 0.6, 1.0), false, 2.0 * ui)
		_text(font, at + Vector2(-8 * zoom, -8 * zoom - 4 * ui), point.label, Color(0.85, 0.6, 1.0), 11)
	if not _pending_warp.is_empty():
		var pending_room: DMRoomLayout = main.layout.get_room(_pending_warp.room)
		var at := to_screen(room_world_rect(pending_room).position + Vector2(_pending_warp.position))
		_overlay.draw_rect(Rect2(at - Vector2(8, 8) * zoom, Vector2(16, 16) * zoom), Color.ORANGE, false, 2.0 * ui)
		_overlay.draw_dashed_line(at, _hover, Color.ORANGE, 1.5 * ui, 5.0 * ui)

	if room.id == main.layout.start_room_id:
		var entrance: Vector2 = p.call(Vector2(main.layout.entrance_point))
		var colour := accent if main.selection.get("type") == "entrance" else Color.GOLD
		_overlay.draw_circle(entrance, 3 * zoom, colour)
		_text(font, entrance + Vector2(4 * zoom + 2 * ui, -2 * ui), "Lonk starts here", colour, 11)

	if main.tool == Tool.PLACE and main.object_scene and room_world_rect(room).has_point(to_world(_hover)):
		var at: Vector2 = p.call(Vector2(snap(room_pixel(room, _hover), main.object_scene, Input.is_key_pressed(KEY_ALT))))
		_overlay.draw_rect(Rect2(at - Vector2(8, 8) * zoom, Vector2(16, 16) * zoom), Color(0.4, 1, 0.5, 0.35))
		_text(font, at + Vector2(10 * zoom, 0), main.object_scene.resource_path.get_file().get_basename(), Color(0.6, 1, 0.7), 12)


# Where a block's link line ends: a shutter's badge, an object or stairs.
func _target_point(room: DMRoomLayout, selection: Dictionary) -> Vector2:
	var origin := room_world_rect(room).position
	match selection.type:
		"door": return door_badge(room, selection.side).get_center()
		"placement":
			var placement := room.get_placement(selection.id)
			return to_screen(origin + Vector2(placement.position)) if placement else Vector2.INF
		"warp": return to_screen(origin + Vector2(selection.warp.from_position))
	return Vector2.INF


func _draw_badge(badge: Dictionary, font: Font, accent: Color) -> void:
	var room: DMRoomLayout = badge.room
	var kind: String = main.door_kind(room, badge.side)
	var colours := { "wall": Color(0.35, 0.35, 0.4, 0.85), "open": Color(0.2, 0.6, 0.3, 0.95), "locked": Color(0.75, 0.6, 0.1, 0.95), "entrance": Color(0.3, 0.5, 0.9, 0.95) }
	var rect: Rect2 = badge.rect
	var selected: bool = main.selection.get("type") == "door" and main.selection.room == room.id and main.selection.side == badge.side
	if not selected and main.selection.get("type") == "door" and main.focused_room_id == -1:
		# In the overview the same doorway can be selected from either room.
		var other: DMRoomLayout = main.layout.get_room(main.selection.room)
		selected = other and main.neighbour_on(other, main.selection.side) == room and DMDungeonLayout.opposite(main.selection.side) == badge.side
	_overlay.draw_rect(rect, colours[kind])
	if rect.grow(2).has_point(_hover) or selected:
		_overlay.draw_rect(rect.grow(1), accent if selected else Color.WHITE, false, 2.0 * ui)
	_overlay.draw_string(font, rect.position + Vector2(2 * ui, rect.size.y * 0.75), door_text(room, badge.side), HORIZONTAL_ALIGNMENT_CENTER, rect.size.x - 4 * ui, int(10 * ui), Color.WHITE)


func _draw_warp_lines(focused: DMRoomLayout) -> void:
	for warp in main.layout.warps:
		var from: DMRoomLayout = main.layout.get_room(warp.from_room_id)
		var to: DMRoomLayout = main.layout.get_room(warp.to_room_id)
		if from == null or to == null or (focused and focused not in [from, to]):
			continue
		var a := to_screen(room_world_rect(from).position + Vector2(warp.from_position))
		var b := to_screen(room_world_rect(to).position + Vector2(warp.arrival_position))
		var colour := Color(0.85, 0.6, 1.0, 0.8)
		if main.selection.get("warp") == warp:
			colour = EditorInterface.get_base_control().get_theme_color("accent_color", "Editor")
		_overlay.draw_dashed_line(a, b, colour, 2.0 * ui, 7.0 * ui)


# Sizes are in unscaled editor pixels.
func _text(font: Font, at: Vector2, text: String, colour: Color, size_px: int) -> void:
	var scaled := int(size_px * ui)
	_overlay.draw_string_outline(font, at, text, HORIZONTAL_ALIGNMENT_LEFT, -1, scaled, int(4 * ui), Color(0, 0, 0, 0.9))
	_overlay.draw_string(font, at, text, HORIZONTAL_ALIGNMENT_LEFT, -1, scaled, colour)
