@tool
extends EditorPlugin

# Adds the "Dungeon" main screen, next to 2D, 3D and Script. Double-clicking a .dungeon.tres opens it there.

const MAIN_SCREEN := preload("res://addons/dungeon_maker/ui/main_screen.tscn")

var main_screen: Control


func _enter_tree() -> void:
	main_screen = MAIN_SCREEN.instantiate()
	main_screen.undo = get_undo_redo()
	EditorInterface.get_editor_main_screen().add_child(main_screen)
	_make_visible(false)


func _exit_tree() -> void:
	# Freed straight away: on editor shutdown a queued free can run after the editor's own resources are gone.
	if is_instance_valid(main_screen):
		main_screen.free()
	main_screen = null


func _has_main_screen() -> bool:
	return true


func _make_visible(visible: bool) -> void:
	if main_screen:
		main_screen.visible = visible


func _get_plugin_name() -> String:
	return "Dungeon"


func _get_plugin_icon() -> Texture2D:
	return EditorInterface.get_base_control().get_theme_icon("TileMapLayer", "EditorIcons")


func _handles(object: Object) -> bool:
	return object is DMDungeonLayout


func _edit(object: Object) -> void:
	if object is DMDungeonLayout and main_screen:
		main_screen.request_open(object)
		EditorInterface.set_main_screen_editor(_get_plugin_name())


func _get_unsaved_status(for_scene: String) -> String:
	if for_scene.is_empty() and main_screen and main_screen.dirty:
		return "Save changes to the dungeon layout before closing?"
	return ""


func _save_external_data() -> void:
	if main_screen and main_screen.dirty and main_screen.layout and not main_screen.layout.resource_path.is_empty():
		main_screen.save_layout(main_screen.layout.resource_path)
