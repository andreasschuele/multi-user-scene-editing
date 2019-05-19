tool
extends EditorPlugin

var addon = null

func _enter_tree():
	addon = load("res://addons/multi-user-scene-editing/MUSE.tscn").instance()
	
	addon.editor_interface = get_editor_interface()
	addon.editor_selection = get_editor_interface().get_selection()
	addon.editor_muse = get_editor_interface().get_editor_muse()
	addon.undo_redo = get_undo_redo()
	
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UR, addon)

func _exit_tree():	
	remove_control_from_docks(addon)
	
	addon.free()