@tool
extends Control

enum SelectionMode { NONE, SET_CROP, ADD_WALL, ADD_OVERHEAD, SELECT_ZONE }
enum ZoneType { NONE, CROP, WALL, OVERHEAD }

# Resources
var map_texture: Texture2D
var reference_tileset: TileSet
var tileset_source_id: int = 1
var default_ground_coord: Vector2i = Vector2i(0, 6)
var tile_size: Vector2i = Vector2i(8, 8)

# Smooth Zooming & Mouse Anchor State
var current_zoom: float = 4.0
var target_zoom: float = 4.0
const MIN_ZOOM: float = 1.0
const MAX_ZOOM: float = 16.0
var zoom_anchor_img_px: Vector2 = Vector2.ZERO
var zoom_anchor_mouse_pos: Vector2 = Vector2.ZERO
var show_grid_overlay: bool = true

# Selections
var current_mode: SelectionMode = SelectionMode.NONE
var crop_region: Rect2i = Rect2i(0, 0, 0, 0)
var wall_regions: Array[Rect2i] = []
var overhead_regions: Array[Rect2i] = []

# Selected Zone State for Editing/Deletion
var active_zone_type: ZoneType = ZoneType.NONE
var active_zone_index: int = -1

# Drag & Pan State
var is_dragging_selection: bool = false
var drag_start_px: Vector2 = Vector2.ZERO
var drag_current_px: Vector2 = Vector2.ZERO

var is_panning: bool = false
var pan_start_pos: Vector2 = Vector2.ZERO

# UI Control Nodes
var scroll_container: ScrollContainer
var texture_rect: TextureRect
var overlay_canvas: Control
var status_label: Label
var active_zone_label: Label
var crop_info_label: Label
var zoom_slider: HSlider
var zoom_label: Label
var output_path_edit: LineEdit
var file_dialog: EditorFileDialog

# Fine-Tuning Zone SpinBoxes & Actions
var spin_zone_x: SpinBox
var spin_zone_y: SpinBox
var spin_zone_w: SpinBox
var spin_zone_h: SpinBox
var btn_delete_zone: Button

func _init() -> void:
	set_anchors_preset(PRESET_FULL_RECT)
	size_flags_horizontal = SIZE_EXPAND_FILL
	size_flags_vertical = SIZE_EXPAND_FILL
	_build_ui()

func _process(delta: float) -> void:
	if not is_equal_approx(current_zoom, target_zoom):
		current_zoom = lerp(current_zoom, target_zoom, 25.0 * delta)
		_update_canvas_size()
		
		var new_scroll = (zoom_anchor_img_px * current_zoom) - zoom_anchor_mouse_pos
		scroll_container.scroll_horizontal = int(new_scroll.x)
		scroll_container.scroll_vertical = int(new_scroll.y)

func _build_ui() -> void:
	file_dialog = EditorFileDialog.new()
	add_child(file_dialog)

	var hsplit = HSplitContainer.new()
	hsplit.set_anchors_preset(PRESET_FULL_RECT)
	add_child(hsplit)

	# --- LEFT SIDEBAR (SCROLLABLE) ---
	var sidebar_scroll = ScrollContainer.new()
	sidebar_scroll.custom_minimum_size = Vector2(330, 0)
	sidebar_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	hsplit.add_child(sidebar_scroll)

	var sidebar_margin = MarginContainer.new()
	sidebar_margin.size_flags_horizontal = SIZE_EXPAND_FILL
	sidebar_margin.size_flags_vertical = SIZE_EXPAND_FILL
	sidebar_margin.add_theme_constant_override("margin_left", 8)
	sidebar_margin.add_theme_constant_override("margin_top", 8)
	sidebar_margin.add_theme_constant_override("margin_right", 8)
	sidebar_margin.add_theme_constant_override("margin_bottom", 8)
	sidebar_scroll.add_child(sidebar_margin)

	var sidebar = VBoxContainer.new()
	sidebar.size_flags_horizontal = SIZE_EXPAND_FILL
	sidebar_margin.add_child(sidebar)

	var title = Label.new()
	title.text = "Zelda Tilemap Tool"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	sidebar.add_child(title)
	sidebar.add_child(HSeparator.new())

	var btn_img = Button.new()
	btn_img.text = "1. Load Map Screenshot"
	btn_img.pressed.connect(_on_load_img_pressed)
	sidebar.add_child(btn_img)

	var btn_ts = Button.new()
	btn_ts.text = "2. Load 8x8 TileSet Resource"
	btn_ts.pressed.connect(_on_load_ts_pressed)
	sidebar.add_child(btn_ts)

	sidebar.add_child(HSeparator.new())

	# --- SELECTION MODE BUTTONS ---
	var btn_select = Button.new()
	btn_select.text = "Select / Edit Zone"
	btn_select.pressed.connect(func(): current_mode = SelectionMode.SELECT_ZONE)
	sidebar.add_child(btn_select)

	var btn_crop = Button.new()
	btn_crop.text = "Draw Crop Area (Green - 1x1 px)"
	btn_crop.pressed.connect(func(): current_mode = SelectionMode.SET_CROP)
	sidebar.add_child(btn_crop)

	var btn_wall = Button.new()
	btn_wall.text = "Draw Wall Subslice (Red - Solid Collision)"
	btn_wall.pressed.connect(func(): current_mode = SelectionMode.ADD_WALL)
	sidebar.add_child(btn_wall)

	var btn_overhead = Button.new()
	btn_overhead.text = "Draw Overhead Subslice (Cyan - Z-Layer 10)"
	btn_overhead.pressed.connect(func(): current_mode = SelectionMode.ADD_OVERHEAD)
	sidebar.add_child(btn_overhead)

	sidebar.add_child(HSeparator.new())

	# --- INSPECTOR / FINE-TUNING PANEL ---
	active_zone_label = Label.new()
	active_zone_label.text = "Selected Zone: None"
	sidebar.add_child(active_zone_label)

	var grid_zone = GridContainer.new()
	grid_zone.columns = 2
	sidebar.add_child(grid_zone)

	grid_zone.add_child(_create_label("Origin X (px):"))
	spin_zone_x = _create_spinbox(0, 4096, _on_zone_spin_changed)
	grid_zone.add_child(spin_zone_x)

	grid_zone.add_child(_create_label("Origin Y (px):"))
	spin_zone_y = _create_spinbox(0, 4096, _on_zone_spin_changed)
	grid_zone.add_child(spin_zone_y)

	grid_zone.add_child(_create_label("Width (px):"))
	spin_zone_w = _create_spinbox(0, 4096, _on_zone_spin_changed)
	grid_zone.add_child(spin_zone_w)

	grid_zone.add_child(_create_label("Height (px):"))
	spin_zone_h = _create_spinbox(0, 4096, _on_zone_spin_changed)
	grid_zone.add_child(spin_zone_h)

	btn_delete_zone = Button.new()
	btn_delete_zone.text = "Delete Selected Subslice"
	btn_delete_zone.disabled = true
	btn_delete_zone.pressed.connect(_delete_active_zone)
	sidebar.add_child(btn_delete_zone)

	var clear_box = HBoxContainer.new()
	sidebar.add_child(clear_box)

	var btn_clear_walls = Button.new()
	btn_clear_walls.text = "Clear Walls"
	btn_clear_walls.size_flags_horizontal = SIZE_EXPAND_FILL
	btn_clear_walls.pressed.connect(_clear_walls)
	clear_box.add_child(btn_clear_walls)

	var btn_clear_overhead = Button.new()
	btn_clear_overhead.text = "Clear Overhead"
	btn_clear_overhead.size_flags_horizontal = SIZE_EXPAND_FILL
	btn_clear_overhead.pressed.connect(_clear_overhead)
	clear_box.add_child(btn_clear_overhead)

	crop_info_label = Label.new()
	crop_info_label.text = "Crop Grid: 0 x 0 tiles"
	sidebar.add_child(crop_info_label)

	sidebar.add_child(HSeparator.new())

	# --- ZOOM CONTROLS ---
	var zoom_header = Label.new()
	zoom_header.text = "Zoom Controls"
	sidebar.add_child(zoom_header)

	var zoom_box = HBoxContainer.new()
	sidebar.add_child(zoom_box)

	zoom_slider = HSlider.new()
	zoom_slider.min_value = MIN_ZOOM
	zoom_slider.max_value = MAX_ZOOM
	zoom_slider.step = 0.1
	zoom_slider.value = target_zoom
	zoom_slider.size_flags_horizontal = SIZE_EXPAND_FILL
	zoom_slider.value_changed.connect(func(v): _trigger_zoom(v, _get_viewport_center()))
	zoom_box.add_child(zoom_slider)

	zoom_label = Label.new()
	zoom_label.text = " %.1fx " % target_zoom
	zoom_box.add_child(zoom_label)

	var grid_chk = CheckBox.new()
	grid_chk.text = "Show 8x8 Tile Grid"
	grid_chk.button_pressed = show_grid_overlay
	grid_chk.toggled.connect(func(t): show_grid_overlay = t; overlay_canvas.queue_redraw())
	sidebar.add_child(grid_chk)

	sidebar.add_child(HSeparator.new())

	# --- OUTPUT PATH SETTINGS ---
	sidebar.add_child(_create_label("Output Scene Path:"))
	var path_box = HBoxContainer.new()
	sidebar.add_child(path_box)

	output_path_edit = LineEdit.new()
	output_path_edit.text = "res://zelda_map_output.tscn"
	output_path_edit.size_flags_horizontal = SIZE_EXPAND_FILL
	path_box.add_child(output_path_edit)

	var btn_browse = Button.new()
	btn_browse.text = "Browse"
	btn_browse.pressed.connect(_on_browse_output_pressed)
	path_box.add_child(btn_browse)

	sidebar.add_child(HSeparator.new())

	status_label = Label.new()
	status_label.text = "Load an image to begin."
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD
	sidebar.add_child(status_label)

	var btn_gen = Button.new()
	btn_gen.text = "Generate TileMap Scene"
	btn_gen.pressed.connect(_generate_map)
	sidebar.add_child(btn_gen)

	# --- RIGHT VIEWPORT PANEL ---
	var viewport_panel = PanelContainer.new()
	viewport_panel.size_flags_horizontal = SIZE_EXPAND_FILL
	viewport_panel.size_flags_vertical = SIZE_EXPAND_FILL
	hsplit.add_child(viewport_panel)

	scroll_container = ScrollContainer.new()
	scroll_container.size_flags_horizontal = SIZE_EXPAND_FILL
	scroll_container.size_flags_vertical = SIZE_EXPAND_FILL
	viewport_panel.add_child(scroll_container)

	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 20)
	margin.add_theme_constant_override("margin_top", 20)
	scroll_container.add_child(margin)

	texture_rect = TextureRect.new()
	texture_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	texture_rect.stretch_mode = TextureRect.STRETCH_SCALE
	texture_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	texture_rect.gui_input.connect(_on_texture_gui_input)
	margin.add_child(texture_rect)

	overlay_canvas = Control.new()
	overlay_canvas.set_anchors_preset(PRESET_FULL_RECT)
	overlay_canvas.mouse_filter = MOUSE_FILTER_IGNORE
	overlay_canvas.draw.connect(_on_overlay_draw)
	texture_rect.add_child(overlay_canvas)

# --- UI HELPERS ---
func _create_label(text: String) -> Label:
	var lbl = Label.new()
	lbl.text = text
	return lbl

func _create_spinbox(min_val: float, max_val: float, callback: Callable) -> SpinBox:
	var sb = SpinBox.new()
	sb.min_value = min_val
	sb.max_value = max_val
	sb.step = 1
	sb.value_changed.connect(callback)
	return sb

func _get_viewport_center() -> Vector2:
	return scroll_container.size / 2.0

func _trigger_zoom(new_target_zoom: float, mouse_viewport_pos: Vector2) -> void:
	zoom_anchor_mouse_pos = mouse_viewport_pos
	var current_scroll = Vector2(scroll_container.scroll_horizontal, scroll_container.scroll_vertical)
	zoom_anchor_img_px = (current_scroll + zoom_anchor_mouse_pos) / current_zoom
	target_zoom = clamp(new_target_zoom, MIN_ZOOM, MAX_ZOOM)

func _update_canvas_size() -> void:
	zoom_label.text = " %.1fx " % target_zoom
	zoom_slider.set_value_no_signal(target_zoom)

	if map_texture:
		var display_size = map_texture.get_size() * current_zoom
		texture_rect.custom_minimum_size = display_size
		overlay_canvas.queue_redraw()

# --- ZONE INSPECTOR & SELECTION MANAGEMENT ---
func _select_zone(type: ZoneType, index: int = -1) -> void:
	active_zone_type = type
	active_zone_index = index

	var r: Rect2i = Rect2i()
	btn_delete_zone.disabled = true

	if type == ZoneType.CROP:
		r = crop_region
		active_zone_label.text = "Selected Zone: Crop Area"
	elif type == ZoneType.WALL and index >= 0 and index < wall_regions.size():
		r = wall_regions[index]
		active_zone_label.text = "Selected Zone: Wall #%d" % (index + 1)
		btn_delete_zone.disabled = false
	elif type == ZoneType.OVERHEAD and index >= 0 and index < overhead_regions.size():
		r = overhead_regions[index]
		active_zone_label.text = "Selected Zone: Overhead #%d" % (index + 1)
		btn_delete_zone.disabled = false
	else:
		active_zone_type = ZoneType.NONE
		active_zone_index = -1
		active_zone_label.text = "Selected Zone: None"

	if active_zone_type != ZoneType.NONE:
		spin_zone_x.set_value_no_signal(r.position.x)
		spin_zone_y.set_value_no_signal(r.position.y)
		spin_zone_w.set_value_no_signal(r.size.x)
		spin_zone_h.set_value_no_signal(r.size.y)

	overlay_canvas.queue_redraw()

func _on_zone_spin_changed(_val: float) -> void:
	if active_zone_type == ZoneType.NONE:
		return

	var updated_rect = Rect2i(
		int(spin_zone_x.value),
		int(spin_zone_y.value),
		int(spin_zone_w.value),
		int(spin_zone_h.value)
	)

	if active_zone_type == ZoneType.CROP:
		crop_region = updated_rect
		var cols = crop_region.size.x / tile_size.x
		var rows = crop_region.size.y / tile_size.y
		crop_info_label.text = "Crop Grid: %d cols x %d rows" % [cols, rows]
	elif active_zone_type == ZoneType.WALL and active_zone_index < wall_regions.size():
		wall_regions[active_zone_index] = updated_rect
	elif active_zone_type == ZoneType.OVERHEAD and active_zone_index < overhead_regions.size():
		overhead_regions[active_zone_index] = updated_rect

	overlay_canvas.queue_redraw()

func _delete_active_zone() -> void:
	if active_zone_type == ZoneType.WALL and active_zone_index >= 0:
		wall_regions.remove_at(active_zone_index)
	elif active_zone_type == ZoneType.OVERHEAD and active_zone_index >= 0:
		overhead_regions.remove_at(active_zone_index)

	_select_zone(ZoneType.NONE)

func _clear_walls() -> void:
	wall_regions.clear()
	if active_zone_type == ZoneType.WALL:
		_select_zone(ZoneType.NONE)
	overlay_canvas.queue_redraw()

func _clear_overhead() -> void:
	overhead_regions.clear()
	if active_zone_type == ZoneType.OVERHEAD:
		_select_zone(ZoneType.NONE)
	overlay_canvas.queue_redraw()

# --- FILE LOADING & SAVING ---
func _on_load_img_pressed() -> void:
	file_dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_FILE
	file_dialog.filters = PackedStringArray(["*.png, *.jpg, *.webp ; Image Files"])
	file_dialog.file_selected.connect(_load_image_file, CONNECT_ONE_SHOT)
	file_dialog.popup_file_dialog()

func _load_image_file(path: String) -> void:
	map_texture = load(path)
	if map_texture:
		texture_rect.texture = map_texture
		_update_canvas_size()
		status_label.text = "Loaded Image: " + path.get_file()

func _on_load_ts_pressed() -> void:
	file_dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_FILE
	file_dialog.filters = PackedStringArray(["*.tres, *.res ; Resource Files"])
	file_dialog.file_selected.connect(_load_tileset_file, CONNECT_ONE_SHOT)
	file_dialog.popup_file_dialog()

func _load_tileset_file(path: String) -> void:
	reference_tileset = load(path) as TileSet
	if reference_tileset:
		status_label.text = "Loaded TileSet: " + path.get_file()

func _on_browse_output_pressed() -> void:
	file_dialog.file_mode = EditorFileDialog.FILE_MODE_SAVE_FILE
	file_dialog.filters = PackedStringArray(["*.tscn ; Packed Scene"])
	file_dialog.file_selected.connect(func(p): output_path_edit.text = p, CONNECT_ONE_SHOT)
	file_dialog.popup_file_dialog()

# --- INPUT HANDLING ---
func _on_texture_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP and event.pressed:
			_trigger_zoom(target_zoom * 1.12, scroll_container.get_local_mouse_position())
			accept_event()
			return
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN and event.pressed:
			_trigger_zoom(target_zoom / 1.12, scroll_container.get_local_mouse_position())
			accept_event()
			return

		if event.button_index == MOUSE_BUTTON_MIDDLE:
			if event.pressed:
				is_panning = true
				pan_start_pos = event.position
			else:
				is_panning = false
			return

	if event is InputEventMouseMotion and is_panning:
		var delta = pan_start_pos - event.position
		scroll_container.scroll_horizontal += int(delta.x)
		scroll_container.scroll_vertical += int(delta.y)
		return

	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		var mouse_px = event.position / current_zoom
		if event.pressed:
			is_dragging_selection = true
			drag_start_px = mouse_px
			drag_current_px = mouse_px
		elif is_dragging_selection:
			is_dragging_selection = false
			drag_current_px = mouse_px
			
			if drag_start_px.distance_to(drag_current_px) < 3.0 or current_mode == SelectionMode.SELECT_ZONE:
				_try_pick_zone_at_point(mouse_px)
			else:
				_commit_selection()
		overlay_canvas.queue_redraw()

	elif event is InputEventMouseMotion and is_dragging_selection:
		drag_current_px = event.position / current_zoom
		overlay_canvas.queue_redraw()

func _try_pick_zone_at_point(px_point: Vector2) -> void:
	var pt = Vector2i(px_point)
	
	for i in range(overhead_regions.size() - 1, -1, -1):
		if overhead_regions[i].has_point(pt):
			_select_zone(ZoneType.OVERHEAD, i)
			return

	for i in range(wall_regions.size() - 1, -1, -1):
		if wall_regions[i].has_point(pt):
			_select_zone(ZoneType.WALL, i)
			return

	if crop_region.has_point(pt):
		_select_zone(ZoneType.CROP)
		return

	_select_zone(ZoneType.NONE)

# --- SELECTION CALCULATIONS ---
func _get_calculated_selection_rect(p1: Vector2, p2: Vector2) -> Rect2i:
	var min_x = int(floor(min(p1.x, p2.x)))
	var max_x = int(floor(max(p1.x, p2.x)))
	var min_y = int(floor(min(p1.y, p2.y)))
	var max_y = int(floor(max(p1.y, p2.y)))

	if current_mode == SelectionMode.SET_CROP:
		var width = max_x - min_x + 1
		var height = max_y - min_y + 1
		return Rect2i(min_x, min_y, width, height)
	else:
		var origin = crop_region.position if crop_region.size != Vector2i.ZERO else Vector2i.ZERO
		
		var rel_min_x = min_x - origin.x
		var rel_max_x = max_x - origin.x
		var rel_min_y = min_y - origin.y
		var rel_max_y = max_y - origin.y

		var start_tile_x = int(floor(float(rel_min_x) / tile_size.x))
		var start_tile_y = int(floor(float(rel_min_y) / tile_size.y))
		var end_tile_x = int(floor(float(rel_max_x) / tile_size.x))
		var end_tile_y = int(floor(float(rel_max_y) / tile_size.y))

		var pos = origin + Vector2i(start_tile_x * tile_size.x, start_tile_y * tile_size.y)
		var sz = Vector2i((end_tile_x - start_tile_x + 1) * tile_size.x, (end_tile_y - start_tile_y + 1) * tile_size.y)
		return Rect2i(pos, sz)

func _commit_selection() -> void:
	var rect = _get_calculated_selection_rect(drag_start_px, drag_current_px)

	if current_mode == SelectionMode.SET_CROP:
		crop_region = rect
		_select_zone(ZoneType.CROP)
	elif current_mode == SelectionMode.ADD_WALL:
		wall_regions.append(rect)
		_select_zone(ZoneType.WALL, wall_regions.size() - 1)
	elif current_mode == SelectionMode.ADD_OVERHEAD:
		overhead_regions.append(rect)
		_select_zone(ZoneType.OVERHEAD, overhead_regions.size() - 1)

	current_mode = SelectionMode.NONE
	overlay_canvas.queue_redraw()

# --- OVERLAY CANVAS DRAWING ---
func _on_overlay_draw() -> void:
	if not map_texture:
		return

	var tex_w = map_texture.get_width()
	var tex_h = map_texture.get_height()

	if show_grid_overlay and current_zoom >= 2.5:
		var grid_col = Color(1, 1, 1, 0.15)
		var start_x = crop_region.position.x % tile_size.x if crop_region.size != Vector2i.ZERO else 0
		var start_y = crop_region.position.y % tile_size.y if crop_region.size != Vector2i.ZERO else 0

		for x in range(start_x, tex_w + 1, tile_size.x):
			var px = x * current_zoom
			overlay_canvas.draw_line(Vector2(px, 0), Vector2(px, tex_h * current_zoom), grid_col, 1.0)

		for y in range(start_y, tex_h + 1, tile_size.y):
			var py = y * current_zoom
			overlay_canvas.draw_line(Vector2(0, py), Vector2(tex_w * current_zoom, py), grid_col, 1.0)

	if is_dragging_selection and current_mode != SelectionMode.SELECT_ZONE:
		var live_rect = _get_calculated_selection_rect(drag_start_px, drag_current_px)
		var display_rect = Rect2(Vector2(live_rect.position) * current_zoom, Vector2(live_rect.size) * current_zoom)
		var col = Color.GREEN
		if current_mode == SelectionMode.ADD_WALL: col = Color.RED
		elif current_mode == SelectionMode.ADD_OVERHEAD: col = Color.CYAN
		
		overlay_canvas.draw_rect(display_rect, Color(col, 0.35), true)
		overlay_canvas.draw_rect(display_rect, col, false, 2.0)

	if crop_region.size != Vector2i.ZERO:
		var display_crop = Rect2(Vector2(crop_region.position) * current_zoom, Vector2(crop_region.size) * current_zoom)
		overlay_canvas.draw_rect(display_crop, Color(0, 1, 0, 0.15), true)
		overlay_canvas.draw_rect(display_crop, Color.GREEN, false, 2.0)

	for i in range(wall_regions.size()):
		var w = wall_regions[i]
		var display_wall = Rect2(Vector2(w.position) * current_zoom, Vector2(w.size) * current_zoom)
		overlay_canvas.draw_rect(display_wall, Color(1, 0, 0, 0.35), true)
		overlay_canvas.draw_rect(display_wall, Color.RED, false, 2.0)

	for i in range(overhead_regions.size()):
		var o = overhead_regions[i]
		var display_oh = Rect2(Vector2(o.position) * current_zoom, Vector2(o.size) * current_zoom)
		overlay_canvas.draw_rect(display_oh, Color(0, 1, 1, 0.35), true)
		overlay_canvas.draw_rect(display_oh, Color.CYAN, false, 2.0)

	var active_rect: Rect2i = Rect2i()
	if active_zone_type == ZoneType.CROP:
		active_rect = crop_region
	elif active_zone_type == ZoneType.WALL and active_zone_index < wall_regions.size():
		active_rect = wall_regions[active_zone_index]
	elif active_zone_type == ZoneType.OVERHEAD and active_zone_index < overhead_regions.size():
		active_rect = overhead_regions[active_zone_index]

	if active_rect.size != Vector2i.ZERO:
		var display_active = Rect2(Vector2(active_rect.position) * current_zoom, Vector2(active_rect.size) * current_zoom)
		overlay_canvas.draw_rect(display_active, Color.YELLOW, false, 3.5)

# --- MAP GENERATION & EXPORT ---
func _generate_map() -> void:
	var save_path = output_path_edit.text.strip_edges()
	if save_path.is_empty():
		status_label.text = "Error: Please specify a valid scene output path!"
		return

	if not map_texture or not reference_tileset or crop_region.size == Vector2i.ZERO:
		status_label.text = "Error: Assign Image, TileSet, and Draw Crop Region first!"
		return

	var atlas_source = reference_tileset.get_source(tileset_source_id) as TileSetAtlasSource
	if not atlas_source or not atlas_source.texture:
		status_label.text = "Error: Invalid TileSetAtlasSource at ID %d" % tileset_source_id
		return

	var src_img: Image = map_texture.get_image()
	var atlas_img: Image = atlas_source.texture.get_image()
	src_img.decompress()
	atlas_img.decompress()

	# Create a duplicated TileSet with zero physics/collision layers for Ground & Overhead
	var non_colliding_tileset: TileSet = reference_tileset.duplicate()
	while non_colliding_tileset.get_physics_layers_count() > 0:
		non_colliding_tileset.remove_physics_layer(0)

	var root_node = Node2D.new()
	root_node.name = output_path_edit.text.get_file().get_basename().to_camel_case()

	# Ground Layer (No Collision)
	var ground_layer = TileMapLayer.new()
	ground_layer.name = "GroundLayer"
	ground_layer.tile_set = non_colliding_tileset
	ground_layer.add_to_group("ground", true)

	# Wall Layer (Solid Collision from reference_tileset)
	var wall_layer = TileMapLayer.new()
	wall_layer.name = "WallLayer"
	wall_layer.tile_set = reference_tileset
	wall_layer.add_to_group("wall", true)

	# Overhead Layer (Z-Layer 10, No Collision)
	var overhead_layer = TileMapLayer.new()
	overhead_layer.name = "OverheadLayer"
	overhead_layer.z_index = 10
	overhead_layer.tile_set = non_colliding_tileset
	overhead_layer.add_to_group("overhead", true)

	root_node.add_child(ground_layer)
	root_node.add_child(wall_layer)
	root_node.add_child(overhead_layer)

	ground_layer.owner = root_node
	wall_layer.owner = root_node
	overhead_layer.owner = root_node

	var atlas_cols = atlas_img.get_width() / tile_size.x
	var atlas_rows = atlas_img.get_height() / tile_size.y
	var grid_cols = crop_region.size.x / tile_size.x
	var grid_rows = crop_region.size.y / tile_size.y

	for ty in range(grid_rows):
		for tx in range(grid_cols):
			var px = crop_region.position.x + (tx * tile_size.x)
			var py = crop_region.position.y + (ty * tile_size.y)
			var cur_rect = Rect2i(px, py, tile_size.x, tile_size.y)

			var best_coord = _find_best_match(src_img, px, py, atlas_img, atlas_cols, atlas_rows)

			var is_wall = false
			for w_rect in wall_regions:
				if w_rect.intersects(cur_rect):
					is_wall = true
					break

			var is_overhead = false
			for o_rect in overhead_regions:
				if o_rect.intersects(cur_rect):
					is_overhead = true
					break

			var target_coord = Vector2i(tx, ty)
			if is_wall:
				wall_layer.set_cell(target_coord, tileset_source_id, best_coord)
				ground_layer.set_cell(target_coord, tileset_source_id, default_ground_coord)
			elif is_overhead:
				overhead_layer.set_cell(target_coord, tileset_source_id, best_coord)
				ground_layer.set_cell(target_coord, tileset_source_id, default_ground_coord)
			else:
				ground_layer.set_cell(target_coord, tileset_source_id, best_coord)

	var output_dir = save_path.get_base_dir()
	if not DirAccess.dir_exists_absolute(output_dir):
		DirAccess.make_dir_recursive_absolute(output_dir)

	var scene = PackedScene.new()
	scene.pack(root_node)
	
	var err = ResourceSaver.save(scene, save_path)
	root_node.queue_free()

	if err == OK:
		status_label.text = "Success! Saved scene to: " + save_path
		EditorInterface.get_resource_filesystem().scan()
	else:
		status_label.text = "Save error code: %d" % err

func _find_best_match(src_img: Image, sx: int, sy: int, atlas_img: Image, cols: int, rows: int) -> Vector2i:
	var min_error: float = INF
	var best_coord: Vector2i = Vector2i.ZERO

	for ay in range(rows):
		for ax in range(cols):
			var error: float = 0.0
			for py in range(tile_size.y):
				for px in range(tile_size.x):
					var c1: Color = src_img.get_pixel(sx + px, sy + py)
					var c2: Color = atlas_img.get_pixel(ax * tile_size.x + px, ay * tile_size.y + py)
					var dr = c1.r - c2.r
					var dg = c1.g - c2.g
					var db = c1.b - c2.b
					error += (dr * dr) + (dg * dg) + (db * db)

					if error >= min_error:
						break
				if error >= min_error:
					break

			if error < min_error:
				min_error = error
				best_coord = Vector2i(ax, ay)

	return best_coord
