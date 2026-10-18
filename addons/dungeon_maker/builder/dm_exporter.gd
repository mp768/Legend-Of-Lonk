@tool
class_name DMExporter
extends RefCounted

# Writes a layout out as a folder: one scene per room, the dungeon scene, and the layout itself.
#
#   <export_dir>/<dungeon>.tscn  <export_dir>/<dungeon>.dungeon.tres  <export_dir>/Rooms/<room>.tscn
#
# Re-exporting keeps hand edits: any node in an existing file without the generated tag is carried over, with
# its connections, into the freshly built scene. Files keep their UIDs, and a file whose content wouldn't
# change isn't rewritten.

const TEMP_FILE := "user://dungeon_maker_export"


static func room_path(layout: DMDungeonLayout, room: DMRoomLayout) -> String:
	return layout.export_dir.path_join("Rooms").path_join(DMNames.file_name(room.room_name) + ".tscn")


static func dungeon_path(layout: DMDungeonLayout) -> String:
	return layout.export_dir.path_join(DMNames.file_name(layout.dungeon_name) + ".tscn")


static func layout_path(layout: DMDungeonLayout) -> String:
	return layout.export_dir.path_join(DMNames.file_name(layout.dungeon_name) + ".dungeon.tres")


# Returns { ok, issues, written, unchanged, orphans }. Orphans are room files no room in the layout produces
# any more. They are listed, never deleted here.
static func export_dungeon(layout: DMDungeonLayout) -> Dictionary:
	var result := { "ok": false, "issues": DMValidator.validate(layout), "written": [], "unchanged": [], "orphans": [] }
	if DMValidator.has_errors(result.issues):
		return result

	var rooms_dir := layout.export_dir.path_join("Rooms")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(rooms_dir))

	# Rooms first, so the dungeon scene can instance them.
	var stamps := DMThemeStamps.new(layout.theme)
	var builder := DMRoomBuilder.new(layout, stamps)
	var room_scenes := {}
	var room_files := {}
	for room in layout.rooms:
		if room.custom_scene:
			continue
		var path := room_path(layout, room)
		room_files[path.get_file()] = true
		if _save_scene(builder.build(room), path, result) != OK:
			stamps.free_stamps()
			return result
		room_scenes[room.id] = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REPLACE)
	stamps.free_stamps()

	if _save_scene(DMDungeonBuilder.build(layout, room_scenes), dungeon_path(layout), result) != OK:
		return result

	if _save_resource(layout, layout_path(layout), result) != OK:
		return result

	for file in DirAccess.get_files_at(rooms_dir):
		if file.ends_with(".tscn") and not room_files.has(file):
			result.orphans.append(rooms_dir.path_join(file))

	_refresh_editor(result.written)
	result.ok = true
	return result


static func _fail(result: Dictionary, message: String) -> void:
	result.issues.append({ "severity": "error", "message": message, "room_id": -1, "edge": null, "placement_id": -1 })


static func _save_scene(root: Node, path: String, result: Dictionary) -> Error:
	var keep_uid := ResourceUID.INVALID_ID
	if FileAccess.file_exists(path):
		keep_uid = ResourceLoader.get_resource_uid(path)
		var old: PackedScene = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
		if old:
			merge_hand_edits(old, root)

	var packed := PackedScene.new()
	var error := packed.pack(root)
	root.free()
	if error != OK:
		_fail(result, "Couldn't pack %s (%s)." % [path, error_string(error)])
		return error

	# Takes over any cached copy, so later loads of this path see the new scene.
	return _save_resource(packed, path, result, keep_uid, true)


# Saves unless the file already holds the same content, and keeps the file's UID.
static func _save_resource(resource: Resource, path: String, result: Dictionary, keep_uid := ResourceUID.INVALID_ID, take_over := false) -> Error:
	if FileAccess.file_exists(path):
		if keep_uid == ResourceUID.INVALID_ID:
			keep_uid = ResourceLoader.get_resource_uid(path)
		var temp := TEMP_FILE + "." + path.get_extension()
		if ResourceSaver.save(resource, temp) == OK and _canonical(FileAccess.get_file_as_string(temp)) == _canonical(FileAccess.get_file_as_string(path)):
			result.unchanged.append(path)
			return OK
	if take_over:
		resource.take_over_path(path)
	var error := ResourceSaver.save(resource, path)
	if error != OK:
		_fail(result, "Couldn't save %s (%s)." % [path, error_string(error)])
		return error
	DMNames.ensure_uid(path, keep_uid)
	result.written.append(path)
	return OK


# Resource text with the parts that differ between two saves of the same content taken out: node unique ids,
# the file's UID, the order of external resources and the generated ids of external and sub-resources.
static func _canonical(text: String) -> String:
	var unique_ids := RegEx.create_from_string(" unique_id=\\d+")
	var uids := RegEx.create_from_string(" uid=\"uid://[a-z0-9]+\"")
	text = uids.sub(unique_ids.sub(text, "", true), "", true)

	# External resources are named by their path instead of their id, and listed in path order.
	var externals := RegEx.create_from_string("\\[ext_resource [^\\n]*path=\"([^\"]+)\" id=\"([^\"]+)\"\\]\\n?")
	var paths := []
	for found in externals.search_all(text):
		paths.append(found.get_string(1))
		text = text.replace("ExtResource(\"%s\")" % found.get_string(2), "ExtResource(\"%s\")" % found.get_string(1))
	text = externals.sub(text, "", true)
	paths.sort()

	var ids := RegEx.create_from_string("\\bid=\"([^\"]+)\"")
	var index := 0
	for found in ids.search_all(text):
		text = text.replace("\"%s\"" % found.get_string(1), "\"#%d\"" % index)
		index += 1
	return "\n".join(paths) + "\n" + text


# Moves every untagged node of the old scene, with its connections, into the freshly built one, under the
# node with the same path (or the root if that node is gone).
static func merge_hand_edits(old_scene: PackedScene, new_root: Node) -> void:
	var old_root := old_scene.instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var kept: Array[Node] = []
	_collect_untagged(old_root, old_root, kept)
	if kept.is_empty():
		old_root.free()
		return

	var moving := {}
	for node in kept:
		for owned in _owned_subtree(node, old_root):
			moving[owned] = true

	# Connections are recorded by path before anything moves, then remade in the new tree.
	var connections := []
	for node in _owned_subtree(old_root, old_root):
		for signal_info in node.get_signal_list():
			for connection in node.get_signal_connection_list(signal_info.name):
				var target = connection.callable.get_object()
				if not (connection.flags & CONNECT_PERSIST) or not (target is Node):
					continue
				if not moving.has(node) and not moving.has(target):
					continue
				connections.append({
					"from": old_root.get_path_to(node),
					"signal": signal_info.name,
					"to": old_root.get_path_to(target),
					"method": connection.callable.get_method(),
					"binds": connection.callable.get_bound_arguments(),
					"flags": connection.flags,
				})

	for node in kept:
		var owned := _owned_subtree(node, old_root)
		var parent := new_root.get_node_or_null(old_root.get_path_to(node.get_parent()))
		for owned_node in owned:
			owned_node.owner = null
		node.get_parent().remove_child(node)
		(parent if parent else new_root).add_child(node)
		for owned_node in owned:
			owned_node.owner = new_root

	for c in connections:
		var from := new_root.get_node_or_null(c.from)
		var to := new_root.get_node_or_null(c.to)
		if from == null or to == null:
			continue
		var callable := Callable(to, c.method)
		if not c.binds.is_empty():
			callable = callable.bindv(c.binds)
		if not from.is_connected(c.signal, callable):
			from.connect(c.signal, callable, c.flags)
	old_root.free()


static func _collect_untagged(node: Node, old_root: Node, kept: Array[Node]) -> void:
	for child in node.get_children():
		if child.owner != old_root:
			continue
		if DMNames.is_tagged(child):
			_collect_untagged(child, old_root, kept)
		else:
			kept.append(child)


# The node and its descendants that belong to `scene_root`'s scene (not the insides of instanced scenes).
static func _owned_subtree(node: Node, scene_root: Node) -> Array[Node]:
	var nodes: Array[Node] = [node]
	for child in node.get_children():
		if child.owner == scene_root:
			nodes.append_array(_owned_subtree(child, scene_root))
	return nodes


static func _refresh_editor(written: Array) -> void:
	if not Engine.is_editor_hint() or not Engine.has_singleton("EditorInterface"):
		return
	var editor = Engine.get_singleton("EditorInterface")
	editor.get_resource_filesystem().scan()
	var open: PackedStringArray = editor.get_open_scenes()
	for path in written:
		if path in open:
			editor.reload_scene_from_path(path)
