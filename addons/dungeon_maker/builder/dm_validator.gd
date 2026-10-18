@tool
class_name DMValidator
extends RefCounted

# Checks a layout before export. Errors block export, warnings don't.
# Each issue: { severity, message, room_id, edge, placement_id }.

const DoorSide := DMDungeonLayout.DoorSide
const Appears := DMPlacement.Appears
const KEYS_EFFECT := 3 # Affectables.EffectType.KEYS

# C# members the builders set, by script. A rename in C# would otherwise break export silently.
const REQUIRED_MEMBERS := {
	DMNames.ROOM_SCRIPT: ["Node2D", ["leftRoom", "rightRoom", "upRoom", "downRoom", "LockUntilCleared", "View"]],
	DMNames.ROOM_MANAGER_SCRIPT: ["Node", ["MainCamera", "StartingRoom", "EntrancePoint", "Music"]],
	DMNames.SHUTTER_SCRIPT: ["TileMapLayer", ["StartsOpen", "Side", "OpensWhenCleared"]],
	DMNames.DOOR_TRIGGER_SCRIPT: ["Node", ["Targets"]],
	DMNames.WARP_SCRIPT: ["Area2D", ["Destination", "Arrival"]],
	DMNames.HIDDEN_ITEM_SCRIPT: ["Node2D", []],
	DMNames.DIALOGUE_TRIGGER_SCRIPT: ["Node", ["Text"]],
	DMNames.LUNGE_STATE_SCRIPT: ["Node", ["LungeSpeed", "ReturnSpeed"]],
}


static func validate(layout: DMDungeonLayout) -> Array[Dictionary]:
	var issues: Array[Dictionary] = []
	_check_assembly(issues)
	if layout.theme == null or layout.theme.stamps == null:
		_add(issues, "error", "The layout has no theme.")
	if layout.get_room(layout.start_room_id) == null:
		_add(issues, "error", "There is no starting room.")

	var names := {}
	for room in layout.rooms:
		var file := DMNames.file_name(room.room_name)
		if file.is_empty():
			_add(issues, "error", "A room has no name.", room.id)
		elif names.has(file):
			_add(issues, "error", "Two rooms are both named '%s'." % room.room_name, room.id)
		names[file] = true
		_check_room(layout, room, issues)

	for edge in layout.edges:
		_check_custom_doors(layout, edge, issues)

	for warp in layout.warps:
		if warp.warp_scene == null or layout.get_room(warp.to_room_id) == null or layout.get_room(warp.from_room_id) == null:
			_add(issues, "error", "A warp has no stairs scene, start or destination.", warp.from_room_id)
		elif warp.appears == Appears.ON_BLOCK_PUSHED and not DMNames.is_block(layout.get_room(warp.from_room_id), warp.block_id):
			_add(issues, "warning", "Stairs wait for a block that no longer exists, so they can never appear.", warp.from_room_id)

	_check_reachable(layout, issues)
	_check_keys(layout, issues)
	return issues


static func has_errors(issues: Array[Dictionary]) -> bool:
	for issue in issues:
		if issue.severity == "error":
			return true
	return false


static func _add(issues: Array[Dictionary], severity: String, message: String, room_id := -1, edge: DMDoorEdge = null, placement_id := -1) -> void:
	issues.append({ "severity": severity, "message": message, "room_id": room_id, "edge": edge, "placement_id": placement_id })


static func _check_assembly(issues: Array[Dictionary]) -> void:
	for path in REQUIRED_MEMBERS:
		var script: Script = load(path) if ResourceLoader.exists(path) else null
		# Non-tool C# scripts can't be instantiated in the editor, but a built one still knows its base type and
		# exported members.
		if script == null or script.get_instance_base_type() == &"":
			_add(issues, "error", "%s can't be used. Build the C# project first." % path.get_file())
			continue
		var node: Node = ClassDB.instantiate(REQUIRED_MEMBERS[path][0])
		node.set_script(script)
		var present := {}
		for property in node.get_property_list():
			present[property.name] = true
		node.free()
		for member in REQUIRED_MEMBERS[path][1]:
			if not present.has(member):
				_add(issues, "error", "%s has no member '%s'. Build the C# project, or update DMNames." % [path.get_file(), member])


static func _check_room(layout: DMDungeonLayout, room: DMRoomLayout, issues: Array[Dictionary]) -> void:
	var counted_enemies := 0
	var blocking := _blocking_brushes(layout)
	for placement in room.placements:
		if placement.scene == null:
			_add(issues, "error", "An object in %s has no scene." % room.room_name, room.id, null, placement.id)
			continue
		var kind := DMNames.kind_of(placement.scene)
		if kind == "enemy" and placement.overrides.get("CountsTowardClear", true):
			counted_enemies += 1
		if placement.appears == Appears.ON_BLOCK_PUSHED and kind != "enemy" and not DMNames.is_block(room, placement.block_id):
			_add(issues, "warning", "%s waits for a block that no longer exists, so it can never appear." % placement.scene.resource_path.get_file(), room.id, null, placement.id)
		if room.custom_scene == null:
			if placement.position.y < DMNames.PLACEABLE_AREA.position.y + DMNames.CELL_SIZE:
				_add(issues, "warning", "An object in %s is in the strip under the top wall." % room.room_name, room.id, null, placement.id)
			elif kind != "block" and blocking.has(room.get_tile(_tile_at(placement.position))):
				_add(issues, "warning", "An object in %s is on a blocking tile." % room.room_name, room.id, null, placement.id)
			if kind == "block":
				_check_block_can_move(room, placement, blocking, issues)

	var has_door := false
	for side in DMDungeonLayout.SIDE_OFFSETS:
		var edge := layout.edge_on(room, side)
		if edge == null:
			continue
		has_door = true
		var rule := edge.shutter_for(room.id)
		if rule.mode == DMShutterRule.Mode.UNTIL_CLEARED and counted_enemies == 0 and room.custom_scene == null:
			_add(issues, "warning", "%s has a shutter that waits for the room to be cleared, but no enemies count toward clearing it." % room.room_name, room.id, edge)
		elif rule.mode == DMShutterRule.Mode.UNTIL_BLOCK_PUSHED and not DMNames.is_block(room, rule.block_id):
			_add(issues, "warning", "%s has a shutter waiting for a block that no longer exists: a soft-lock." % room.room_name, room.id, edge)
	for placement in room.placements:
		if placement.appears == Appears.ON_ROOM_CLEARED and counted_enemies == 0:
			_add(issues, "warning", "An object in %s appears when the room is cleared, but nothing counts toward clearing it, so it appears straight away." % room.room_name, room.id, null, placement.id)

	var has_warp := false
	for warp in layout.warps:
		if warp.from_room_id == room.id or warp.to_room_id == room.id:
			has_warp = true
	var is_entrance := room.id == layout.start_room_id
	if not has_door and not has_warp and not is_entrance:
		_add(issues, "warning", "%s has no doors and no warps." % room.room_name, room.id)


static func _blocking_brushes(layout: DMDungeonLayout) -> Dictionary:
	var blocking := {}
	if layout.theme == null or layout.theme.stamps == null:
		return blocking
	var stamps := DMThemeStamps.new(layout.theme)
	for i in stamps.brushes.size():
		if stamps.brush_blocks(i):
			blocking[i] = true
	stamps.free_stamps()
	return blocking


static func _tile_at(position: Vector2i) -> Vector2i:
	var tile := (position - DMNames.INTERIOR_ORIGIN * DMNames.CELL_SIZE) / DMNames.TILE_SIZE
	return tile.clamp(Vector2i.ZERO, Vector2i(DMRoomLayout.TILES_WIDE - 1, DMRoomLayout.TILES_HIGH - 1))


static func _check_block_can_move(room: DMRoomLayout, placement: DMPlacement, blocking: Dictionary, issues: Array[Dictionary]) -> void:
	var direction: Vector2 = placement.overrides.get("AllowedDirection", Vector2.ZERO)
	if direction == Vector2.ZERO:
		return
	var target := _tile_at(placement.position) + Vector2i(direction)
	if not DMRoomLayout.in_bounds(target) or blocking.has(room.get_tile(target)):
		_add(issues, "warning", "A block in %s can't move in its allowed direction." % room.room_name, room.id, null, placement.id)


# A locked door or shutter on a custom room needs a door node of that kind, with that Side, in its scene.
static func _check_custom_doors(layout: DMDungeonLayout, edge: DMDoorEdge, issues: Array[Dictionary]) -> void:
	for room_id in [edge.room_a, edge.room_b]:
		var room := layout.get_room(room_id)
		if room == null or room.custom_scene == null:
			continue
		var other := layout.get_room(edge.other_room(room_id))
		var side := layout.side_between(room, other)
		var game_side := side - 1 # Room.Side has no NONE
		var needs := []
		if edge.kind == DMDoorEdge.Kind.LOCKED:
			needs.append(DMNames.LOCKED_DOOR_SCRIPT)
		if edge.shutter_for(room_id).mode != DMShutterRule.Mode.NONE:
			needs.append(DMNames.SHUTTER_SCRIPT)
		if needs.is_empty():
			continue
		var instance := room.custom_scene.instantiate()
		for script_path in needs:
			var found := false
			for child in instance.get_children():
				var script: Script = child.get_script()
				if script and script.resource_path == script_path and int(child.get(DMNames.DOOR_SIDE)) == game_side:
					found = true
			if not found:
				_add(issues, "error", "Custom room %s has no %s with Side %s." % [room.room_name, script_path.get_file().get_basename(), DMDungeonLayout.side_name(side)], room.id, edge)
		instance.free()


# Breadth-first search over doors and warps from the starting room.
static func _check_reachable(layout: DMDungeonLayout, issues: Array[Dictionary]) -> void:
	if layout.get_room(layout.start_room_id) == null:
		return
	var seen := { layout.start_room_id: true }
	var queue := [layout.start_room_id]
	while not queue.is_empty():
		var room_id: int = queue.pop_front()
		var next := []
		for edge in layout.edges:
			if edge.has_room(room_id):
				next.append(edge.other_room(room_id))
		for warp in layout.warps:
			if warp.from_room_id == room_id:
				next.append(warp.to_room_id)
			if warp.two_way and warp.to_room_id == room_id:
				next.append(warp.from_room_id)
		for other in next:
			if not seen.has(other):
				seen[other] = true
				queue.append(other)
	for room in layout.rooms:
		if not seen.has(room.id):
			_add(issues, "warning", "%s can't be reached from the start." % room.room_name, room.id)


# A hint, not a proof: counts key pickups (hidden or not) and key drop tables against locked doors.
static func _check_keys(layout: DMDungeonLayout, issues: Array[Dictionary]) -> void:
	var locked := 0
	for edge in layout.edges:
		if edge.kind == DMDoorEdge.Kind.LOCKED:
			locked += 1
	var keys := 0
	for room in layout.rooms:
		for placement in room.placements:
			if placement.scene == null:
				continue
			if DMNames.kind_of(placement.scene) == "collectible" and int(DMNames.root_property(placement.scene, &"Type", -1)) == KEYS_EFFECT:
				keys += int(placement.overrides.get("Value", DMNames.root_property(placement.scene, &"Value", 1)))
			var drops: Resource = placement.overrides.get("Drops")
			if drops and _drops_key(drops):
				keys += 1
	if locked > keys:
		_add(issues, "warning", "There are %d locked doors but only %d keys can be found." % [locked, keys])


static func _drops_key(drops: Resource) -> bool:
	var entries = drops.get("Entries")
	if entries == null:
		return false
	for entry in entries:
		var item: PackedScene = entry.get("Item") if entry else null
		if item and int(DMNames.root_property(item, &"Type", -1)) == KEYS_EFFECT:
			return true
	return false
