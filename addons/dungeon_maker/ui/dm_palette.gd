@tool
extends VBoxContainer

# Brushes from the theme's stamp scene, and objects from the theme's entity folders, grouped by folder.
# Click a brush to paint with it. Click an object to place it once (then you're back to Select), or drag it
# straight into a room.

const Tool := preload("res://addons/dungeon_maker/ui/dm_main_screen.gd").Tool

const KIND_HELP := {
	"enemy": "Enemy. Always there when the room is entered, and counts toward clearing the room unless you turn that off.",
	"collectible": "Pickup. Can be there from the start, appear when the room is cleared, or appear when a block is pushed.",
	"block": "Pushable block. Can open shutters, and reveal pickups or stairs when it's pushed.",
	"warp": "Stairs. Use the Warp tool to place stairs with a destination.",
	"other": "World object.",
}

@export var main_path: NodePath
var main: Node

var _brushes: ItemList
var _objects: Tree
var _theme: DMDungeonTheme


func _ready() -> void:
	main = get_node(main_path)
	if main == null or EditorInterface.get_edited_scene_root() == owner:
		return
	var brush_label := Label.new()
	brush_label.text = "Floor brushes"
	brush_label.tooltip_text = "Click a brush to paint it onto the room's floor tiles. The tile layers decide collision."
	brush_label.mouse_filter = Control.MOUSE_FILTER_PASS
	add_child(brush_label)
	_brushes = ItemList.new()
	_brushes.auto_height = true
	_brushes.max_columns = 2
	_brushes.same_column_width = true
	_brushes.fixed_icon_size = Vector2i(24, 24) * EditorInterface.get_editor_scale()
	_brushes.item_selected.connect(_on_brush_selected)
	add_child(_brushes)

	var object_label := Label.new()
	object_label.text = "Objects"
	object_label.tooltip_text = "Click to place one, or drag into a room. Hold Shift while placing to place several."
	object_label.mouse_filter = Control.MOUSE_FILTER_PASS
	add_child(object_label)
	_objects = Tree.new()
	_objects.hide_root = true
	_objects.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_objects.custom_minimum_size.y = 160
	_objects.item_selected.connect(_on_object_selected)
	_objects.set_drag_forwarding(_get_object_drag, Callable(), Callable())
	add_child(_objects)

	main.layout_changed.connect(_refresh)
	main.tool_changed.connect(_on_tool_changed)
	_refresh()


func _refresh() -> void:
	if main.layout == null or main.layout.theme == _theme or main.stamps == null:
		return
	_theme = main.layout.theme
	_brushes.clear()
	_objects.clear()
	for i in main.stamps.brushes.size():
		var brush: Node = main.stamps.brushes[i]
		var index := _brushes.add_item(brush.name.capitalize(), _brush_icon(brush))
		var tip: String = brush.editor_description if not brush.editor_description.is_empty() else brush.name
		_brushes.set_item_tooltip(index, tip)

	var root := _objects.create_item()
	for folder in _theme.entity_folders:
		var group := _objects.create_item(root)
		group.set_text(0, folder.trim_suffix("/").get_file())
		group.set_selectable(0, false)
		for file in DirAccess.get_files_at(folder):
			if not file.ends_with(".tscn"):
				continue
			var path := folder.path_join(file)
			var item := _objects.create_item(group)
			item.set_text(0, file.get_basename().capitalize())
			item.set_metadata(0, path)
			item.set_tooltip_text(0, "%s\n%s" % [KIND_HELP[DMNames.kind_of(load(path))], path])
			EditorInterface.get_resource_previewer().queue_resource_preview(path, self, &"_on_preview", item)


# The brush's own tiles, as they'll be painted (colliding layers on top).
func _brush_icon(brush: Node) -> Texture2D:
	var image := Image.create(16, 16, false, Image.FORMAT_RGBA8)
	for layer_name in ["GroundLayer", "WallLayer", "LowWallLayer"]:
		var layer: TileMapLayer = brush.get_node_or_null(layer_name)
		if layer == null:
			continue
		for cell in layer.get_used_cells():
			var source := layer.tile_set.get_source(layer.get_cell_source_id(cell)) as TileSetAtlasSource
			var atlas := source.texture.get_image()
			if atlas.is_compressed():
				atlas.decompress()
			atlas.convert(Image.FORMAT_RGBA8)
			var region := source.get_tile_texture_region(main.stamps.map_coords(layer.get_cell_atlas_coords(cell)))
			image.blend_rect(atlas, region, cell * 8)
	return ImageTexture.create_from_image(image)


func _on_preview(_path: String, preview: Texture2D, _thumbnail: Texture2D, item: TreeItem) -> void:
	if preview and is_instance_valid(_objects):
		item.set_icon(0, preview)
		item.set_icon_max_width(0, 24)


func _on_brush_selected(index: int) -> void:
	main.brush_index = index
	if main.tool not in [Tool.PAINT, Tool.RECT]:
		main.set_tool(Tool.PAINT)
	if main.focused_room_id == -1:
		main.set_status("Brush picked. Click a room to paint in it.")


func _on_object_selected() -> void:
	var item := _objects.get_selected()
	if item == null or item.get_metadata(0) == null:
		return
	main.set_tool(Tool.PLACE, load(item.get_metadata(0)))
	if main.focused_room_id == -1:
		main.set_status("Click a room first, then click where the object goes. Or drag it into a room.")


# Once placing is over, the object list lets go of its selection so the same object can be picked again.
func _on_tool_changed() -> void:
	if main.tool != Tool.PLACE and _objects.get_selected():
		_objects.deselect_all()


func _get_object_drag(at: Vector2) -> Variant:
	var item := _objects.get_item_at_position(at)
	if item == null or item.get_metadata(0) == null:
		return null
	var preview := Label.new()
	preview.text = item.get_text(0)
	set_drag_preview(preview)
	return { "dm_scene": item.get_metadata(0) }
