tool
extends Panel

export(Texture) var folder_icon

onready var server_url_linedit = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer/ServerHostLineEdit
onready var username_lineedit = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer/UsernameLineEdit
onready var session_lineedit = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer/SessionLineEdit
onready var session_password_lineedit = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer/SessionPasswortLineEdit
onready var session_file_sync_checkbox = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer/HBoxContainer2/FileSyncCheckBox
onready var connect_btn = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer/ConnectButton
onready var participant_itemlist = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer4/ItemList
onready var color_picker_btn = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer/HBoxContainer/ColorPickerButton
onready var debug_test_btn = $MarginContainer/PanelContainer/HBoxContainer/Controls/ScrollContainer/VBoxContainer/VBoxContainer3/DoSomething

# Injected by the main plugin script.

var editor_interface : EditorInterface 
var editor_selection : EditorSelection
var editor_muse : EditorMUSE

# An array of dummy usernames.

var g_usernames = [
	"Homer", "Marge", "Bart", "Lisa", "Maggie", "Selma", "Patty",
	"Kent", "Carl", "Crazy Cat Lady", "Comic Book Guy", "Ned", "Milhouse",
	"Todd", "Rod", "Ralph", "Barney", "Krusty", "Lenny", "Snowball"
]

# An array of easily distinguishable colors.

var g_user_colors = [
	Color("fd27ff"),
	Color("ff5656"),
	Color("4727ff"),
	Color("1eff61"),
	Color("beff1e"),
	Color("ff9d1e"),
	Color("8f6d48")
]

# Settings for testing

var g_debug = true

# Stuff

var undo_redo : UndoRedo
var outboxMessageIdCounter: int = 0


func _ready():
	self.set_focus_mode(Control.FOCUS_ALL)
	
	# Initialise some components
	
	randomize()
	
	var random_username = g_usernames[rand_range(0, len(g_usernames))]
	username_lineedit.text = random_username
	
	var random_color = g_user_colors[rand_range(0, len(g_user_colors))]
	random_color = random_color * rand_range(0.5, 1)
	random_color[3] = 1	
	color_picker_btn.color = random_color
	
	muse_log_debug("Components initialised.")
	
	# Register callback handler.
	
	undo_redo.clear_callback()
	undo_redo.add_callback(self, "muse_undo_redo_callback")
	
	editor_selection.clear_selection_changed_callback()
	editor_selection.add_selection_changed_callback(self, "muse_selection_changed_callback")
	
	editor_muse.clear_update_node_property_callbacks()
	editor_muse.add_update_node_property_callback(self, "muse_node_update_property_callback")
	
	editor_muse.clear_event_callbacks()
	editor_muse.add_event_callback(self, "muse_event_callback")
	
	muse_log_debug("Callbacks registred.")
	
	# Connect with buttons.

	connect_btn.connect("button_down", self, "_on_connect_button_down")
	debug_test_btn.connect("button_down", self, "_on_debug_test_button_down")
	
	muse_log_debug("Buttons connected.")
 

func _on_connect_button_down():
	if connect_btn.text == "Connect":
		var result = muse_connect(server_url_linedit.text)
		
		muse_log(result)
		
		if result == "Connected":
			connect_btn.text = "Disconnect"
	else:
		# Tell to stop the MUSE thread.
		muse_disconnect()

		# Wait until the MUES client has disconnected.

		var t = Timer.new()
		t.set_wait_time(0.1)
		t.set_one_shot(true)
		self.add_child(t)
		
		while muse_ws_client != null:
			t.start()
			yield(t, "timeout")

		self.remove_child(t)
		t.queue_free()
		
		connect_btn.text = "Connect"


#################################
# MUSE
#
# This section contains the core MUSE implementation.
#
#################################

var muse_ws_client : WebSocketClient = null
var muse_thread : Thread = null
var muse_handler_thread : Thread = null
var muse_thread_stop : bool = false

var muse_message_handlers = []
var muse_message_handler_in_pool = []

var muse_ping_enabled: bool = true
var muse_ping_last_msg: int = OS.get_ticks_msec()

var g_muse_state = {}

var ignore_node_on_add = null
var ignore_node_on_remove = null

func muse_connect(url):
	if (muse_ws_client != null):
		return
		
	# Reset internal state.
	
	outboxMessageIdCounter = 0

	# Register message handlers.
	
	muse_message_handlers.clear()
		
	muse_message_handlers.append({ "type": "Ping", "handler": "muse_message_handler_ping" })
	muse_message_handlers.append({ "type": "ConnectionConfirm", "handler": "muse_message_handler_connection_confirm" })
	muse_message_handlers.append({ "type": "SessionJoinDeclined", "handler": "muse_message_handler_session_join_declined" })
	muse_message_handlers.append({ "type": "SessionJoined", "handler": "muse_message_handler_session_joined" })	
	muse_message_handlers.append({ "type": "SessionUserList", "handler": "muse_message_handler_participant_list" })
	muse_message_handlers.append({ "type": "SceneSynchronisation", "handler": "muse_message_handler_scene_synchronization" })
	muse_message_handlers.append({ "type": "NodeAdd", "handler": "muse_message_handler_node_add" })
	muse_message_handlers.append({ "type": "NodeRemove", "handler": "muse_message_handler_node_remove" })
	muse_message_handlers.append({ "type": "NodeUpdate", "handler": "muse_message_handler_node_update" })
	muse_message_handlers.append({ "type": "ResourceListRequest", "handler": "muse_message_handler_resource_list_request" })
	muse_message_handlers.append({ "type": "ResourceRequest", "handler": "muse_message_handler_resource_request" })
	muse_message_handlers.append({ "type": "ResourceUpload", "handler": "muse_message_handler_ressource_upload" })
	
	# Connect

	muse_ws_client = WebSocketClient.new()

	muse_ws_client.connect("connection_established", self, "_muse_ws_client_connection_established")
	muse_ws_client.connect("connection_closed", self, "_muse_ws_client_connection_closed")
	muse_ws_client.connect("connection_error", self, "_muse_ws_client_connection_error")
	muse_ws_client.connect("data_received", self, "_muse_ws_client_data_received")

	var result = muse_ws_client.connect_to_url(url)

	muse_ws_client.get_my_peer().set_write_mode(WebSocketPeer.WRITE_MODE_TEXT)

	if (result == OK):
		muse_log("Connected!")
		
		muse_thread_stop = false

		muse_thread = Thread.new()
		muse_thread.start(self, "_muse_ws_thread_run", "...")
		
		muse_handler_thread = Thread.new()
		muse_handler_thread.start(self, "_muse_ws_handler_thread_run", "...")

		return "Connected"

	return "Not-Connected"

func muse_disconnect():
	muse_thread_stop = true
	
func muse_log(message):
	print(message)
	
func muse_log_debug(message):
	if g_debug:
		print(str("[DEBUG] ", message))

func _muse_thread_end():
	muse_thread.wait_to_finish()
	muse_thread = null
	muse_ws_client = null

	muse_log("Thread ended")
	
	# Clean up...
	
	var scene = get_tree().get_edited_scene_root()
	
	participant_itemlist.clear()
	#editor_muse.free_locks(scene)
	editor_muse.update_tree()	

func _muse_ws_thread_run(args: String):
	muse_log("Thread started...")
	
	while (!muse_thread_stop):
		muse_ws_client.poll()
		
		if muse_ping_enabled and muse_ping_last_msg + 1000 < OS.get_ticks_msec():
			muse_send_message("Ping", {
				"timestamp": OS.get_ticks_msec()
			})
			muse_ping_last_msg = OS.get_ticks_msec()

	muse_ws_client.disconnect_from_host()

	call_deferred("_muse_thread_end")

func _muse_ws_handler_thread_run(args: String):
	muse_log("Handler-Thread started...")

	while (!muse_thread_stop):
		if muse_message_handler_in_pool.size() == 0:
			continue

		var json = muse_message_handler_in_pool.pop_front()

		for handler in muse_message_handlers:
			if handler.type == json._type:
				call(handler.handler, json)

func _muse_ws_client_connection_established(protocol):
	muse_log("Connection established")

	muse_log("Send authorization...")

	muse_send_message("ConnectionRequest", {
		"magic": "#This is a magic string.#"
	})

func _muse_ws_client_connection_closed():
	muse_log("Connection closed")
	muse_thread_stop = true

func _muse_ws_client_connection_error():
	muse_log("Connection error")
	muse_thread_stop = true

func _muse_ws_client_data_received():
	var peer = muse_ws_client.get_my_peer()
	var packet_bytes = peer.get_packet()
	var message = packet_bytes.get_string_from_utf8()

	if message != null and message.length() < 1000:
		muse_log_debug("Received message: " + message)
	else:
		muse_log_debug("Received message: " + message.substr(0, 1000))

	if message.find("MUSE:") != 0:
		return

	var json = JSON.parse(message.right(5)).result

	if typeof(json) != TYPE_DICTIONARY:
		return
		
	muse_message_handler_in_pool.push_back(json)

func muse_send_utf8_string(message):
	if message == null:
		return
	
	if message.length() < 1000:
		muse_log_debug("Send message: " + message)
	else:
		muse_log_debug("Send message: " + message.substr(0, 1000))
	
	if (muse_ws_client != null
		and muse_ws_client.get_my_peer() != null 
		and muse_ws_client.get_my_peer().is_connected_to_host()):

		var peer = muse_ws_client.get_my_peer()
		peer.put_packet(message.to_utf8())
		
func muse_prepare_message(type, payload = null):
	outboxMessageIdCounter = outboxMessageIdCounter + 1
	
	var message = {
		"_smid": outboxMessageIdCounter,
		"_type": type,
		"_time": OS.get_ticks_msec()
	}
	
	if payload != null:
		for k in payload.keys():
			message[k] = payload[k]
		
	return message

func muse_send_message_direct(message):
	var json_message = JSON.print(message)

	muse_send_utf8_string("MUSE:" + json_message)

func muse_send_message(type, payload = null):
	var message = muse_prepare_message(type, payload)

	muse_send_message_direct(message)


func muse_send_file(file_name, file_path, with_content: bool = false):
	muse_log_debug(str("File: ", file_path))
	
	var file = File.new()
	file.open(file_path, File.READ)

	var md5 = file.get_md5(file_path)
	
	muse_log_debug(str("File-MD5: ", md5))
	muse_log_debug(str("File-length: ", file.get_len()))
	
	var bytes = null
	var bytes_base64 = null

	if with_content:
		bytes = file.get_buffer(file.get_len())
		bytes_base64 = Marshalls.raw_to_base64(bytes)

	file.close()

	if with_content:
		muse_send_message("ResourceUpload", {
			"fileName": file_name,
			"filePath": file_path,
			"content": bytes_base64,
			"hash": md5
		})
	else:
		muse_send_message("ResourceUpload", {
			"fileName": file_name,
			"filePath": file_path,
			"hash": md5
		})

func muse_send_all_files(with_content: bool = false):
	var editor_filesystem = editor_interface.get_resource_filesystem() 
	var editor_filesystem_directory = editor_filesystem.get_filesystem() 

	var directories = [ editor_filesystem_directory ]  

	while directories.empty() == false:
		var directory = directories.pop_front()
	
		# Push all subdirectories.
		
		for idx in range(directory.get_subdir_count()):
			directories.push_front(directory.get_subdir(idx))

		# Go trough all files.

		var file_count = directory.get_file_count()

		for idx in range(file_count):
			var file_name = directory.get_file(idx)
			var file_path = directory.get_file_path(idx)
			
			muse_send_file(file_name, file_path, with_content)
			
			_delay()


func _delay():
	var x = 0
	for i in range(10000):
		x = x + 1

#################################
# MUSE
#
# This section contains MUSE message handlers and helper functions.
#
#################################

func muse_message_handler_ping(json):
	var current_timestamp = OS.get_ticks_msec()
	var sent_timestamp = json.timestamp
	var ping_in_ms = current_timestamp - sent_timestamp
	
	muse_log(str("PING: ", ping_in_ms, "ms"))

func muse_message_handler_connection_confirm(json):
	muse_log("Connection confirmed.")
	
	var session = session_lineedit.text
	var session_password = session_password_lineedit.text
	var session_file_sync = session_file_sync_checkbox.pressed
	var username = username_lineedit.text
	var usercolor = color_picker_btn.color
	
	muse_send_message("SessionJoin", {
		"session": session,
		"sessionPassword": session_password,
		"sessionFileSync": session_file_sync,
		"username": username,
		"userColor": Marshalls.variant_to_base64(usercolor)
	})
	
	g_muse_state["session"] = session
	g_muse_state["sessionPassword"] = session_password
	g_muse_state["sessionFileSync"] = session_file_sync
	g_muse_state["username"] = username
	g_muse_state["userColor"] = usercolor
	
	# Pass username and color to the native MUSE implementation.
	editor_muse.set_user_color(username, usercolor)
	editor_muse.set_current_user(username)
	
func muse_message_handler_session_join_declined(json):
	# Simulate a button click.
	_on_connect_button_down()

func muse_message_handler_session_joined(json):
	if json.owner == true:
		# As a session owner upload the whole scene to the server.
		
		var scene_nodes = export_tree()
	
		for e in scene_nodes:
			muse_send_message_direct(e)
	
		# Also upload project the project files.
		
		if g_muse_state["sessionFileSync"]:
			muse_send_all_files()
	else:
		# Request for synchronisation as the user is not the owner of the scene.
		muse_send_message("SceneSynchronisationRequest")

func muse_message_handler_scene_synchronization(json):
	call_deferred("muse_message_handler_scene_synchronization_deferred", json)
	
func muse_parse_type_to_class(type: String):
	return type.right(type.find(":") + 1)
	
func muse_get_property_data(properties, name):
	for p in properties:
		if p.name == name:
			return p.data
			
	return null
	
func muse_message_handler_scene_synchronization_deferred(json):
	if json.parentSelector == "":
		editor_muse.new_scene(muse_parse_type_to_class(json.type), json.name)
	else:
		var scene = get_tree().get_edited_scene_root()
		var node = get_scene_node_from_path(json.parentSelector)
		
		if node == null:
			muse_log(str("WARNING: Parent node not found in scene. Node: ", node))
			return
		
		muse_log_debug(str("Parent node:", node))
	
		var new_object = null
		
		if json.properties != null and muse_get_property_data(json.properties, "PakedScene") != null:
			var packed_scene_filename = muse_get_property_data(json.properties, "PakedScene") 
			var packed_scene = load(packed_scene_filename)
			new_object = packed_scene.instance()
			muse_log(str("instantiate ", packed_scene_filename))
		else:
			new_object = ClassDB.instance(muse_parse_type_to_class(json.type))
		
		new_object.name = json.name
		
		node.add_child(new_object)
		
		# The owner is the scene which instanciated the object.
		new_object.set_owner(scene)
		
		for p in json.properties:
			# Ignore specific properties.
			if p.name == "PakedScene" or p.name == "MeshFile":
				continue
				
			new_object.set(p.name, Marshalls.base64_to_variant(p.data))
		
		if new_object is MeshInstance:
			var mesh_resource_path = muse_get_property_data(json.properties, "MeshFile")
			
			new_object.mesh = load(mesh_resource_path)
	
			var newMaterial = ClassDB.instance("SpatialMaterial")
			new_object.set_surface_material(0, newMaterial);


func muse_message_handler_resource_list_request(json):
	var editor_filesystem = editor_interface.get_resource_filesystem() 
	
	var editor_filesystem_directory = editor_filesystem.get_filesystem() 

	var directories = [ editor_filesystem_directory ]  

	while directories.empty() == false:
		var directory = directories.pop_front()
		
		# Push all subdirectories.
		
		for idx in range(directory.get_subdir_count()):
			directories.push_front(directory.get_subdir(idx))

		# Go trough all files.

		var file_count = directory.get_file_count()

		for idx in range(file_count):
			var file_name = directory.get_file(idx)
			var file_path = directory.get_file_path(idx)
			
			if (json.filePath != file_path):
				continue

			muse_send_file(file_name, file_path)

func muse_message_handler_resource_request(json):
	var editor_filesystem = editor_interface.get_resource_filesystem() 
	
	var editor_filesystem_directory = editor_filesystem.get_filesystem() 

	var directories = [ editor_filesystem_directory ]  

	while directories.empty() == false:
		var directory = directories.pop_front()
		
		# Push all subdirectories.
		
		for idx in range(directory.get_subdir_count()):
			directories.push_front(directory.get_subdir(idx))

		# Go trough all files.

		var file_count = directory.get_file_count()

		for idx in range(file_count):
			var file_name = directory.get_file(idx)
			var file_path = directory.get_file_path(idx)
			
			if (json.filePath != file_path):
				continue

			muse_send_file(file_name, file_path, true)

func muse_message_handler_node_add(json):
	var scene = get_tree().get_edited_scene_root()
	var node = get_scene_node_from_path(json.parentSelector)
	muse_log_debug(str("LOG-NODE:", node))

	var new_object = null
	
	if json.properties != null and muse_get_property_data(json.properties, "PakedScene") != null:
		var packed_scene_filename = muse_get_property_data(json.properties, "PakedScene")  
		var packed_scene = load(packed_scene_filename)
		new_object = packed_scene.instance()
	else:
		new_object = ClassDB.instance(muse_parse_type_to_class(json.type))
	
	new_object.name = json.name
	
	node.add_child(new_object)
	
	# The owner is the scene which instanciated the object.
	new_object.set_owner(scene)
	
	for p in json.properties:
		# Ignore specific properties.
		if p.name == "PakedScene" or p.name == "MeshFile":
			continue
		
		new_object.set(p.name, Marshalls.base64_to_variant(p.data))
	
	if new_object is MeshInstance:
		var mesh_resource_path = muse_get_property_data(json.properties, "MeshFile")
		
		new_object.mesh = load(mesh_resource_path)

		var newMaterial = ClassDB.instance("SpatialMaterial")
		new_object.set_surface_material(0, newMaterial);
	
func muse_message_handler_node_remove(json):
	var scene = get_tree().get_edited_scene_root()
	
	var node_path_node = null
	
	if json.selector == scene.name:
		node_path_node = scene
	else:
		# Node2D/cat5
		node_path_node = scene.get_node(json.selector.right(scene.name.length() + 1))
	
	node_path_node.get_parent().remove_child(node_path_node)

func muse_message_handler_participant_list(json):
	participant_itemlist.clear()

	for e in json.users:
		participant_itemlist.add_item(e.username)
		
		var item_idx = participant_itemlist.get_item_count () - 1
		var item_color = Marshalls.base64_to_variant(e.color)
		
		participant_itemlist.set_item_custom_fg_color(item_idx, item_color)
		
		# Set user color in the native MUES components
		
		editor_muse.set_user_color(e.username, item_color)

func muse_message_handler_ChangeProperty(json, data):	
	var node = get_scene_node_from_path(data.node_path)
	node.set(data.property, Marshalls.base64_to_variant(data.variant_base64))

func muse_message_handler_node_update(json):
	var node_path = json.selector
	
	muse_log_debug("Select node " + node_path)

	var node = get_scene_node_from_path(node_path)
	
	for p in json.properties:
		node.set(p.name, Marshalls.base64_to_variant(p.data))
		
		if p.name == "locked_by":
			call_deferred("muse_update_tree_deffered", node)

func muse_message_handler_ressource_upload(json):
	muse_log_debug(str("Adding a new file to file system: ", json.filePath))
	
	var file = File.new()
	
	if file.file_exists(json.filePath):
		muse_log_debug(str("File exists."))
		return
	
	var bytes = Marshalls.base64_to_raw(json.content)
	
	file.open(json.filePath, File.WRITE)
	file.store_buffer(bytes)
	file.close()
	
	var editor_filesystem = editor_interface.get_resource_filesystem() 
	editor_filesystem.scan()
	editor_filesystem.update_file(json.filePath)
	
func muse_message_handler_ChangeTexture(json):
	var node_path = json.payload.node_path
	var resource_path = json.payload.resource_path

	muse_log_debug("Select node: " + node_path)
	muse_log_debug("resource_path: " + resource_path)

	var node = get_scene_node_from_path(node_path)
	
	var sprite : Sprite = node
	
	if ResourceLoader.exists(resource_path):
		sprite.texture = load(resource_path)
	else:
		muse_send_message("RequestFile", { 
			"filePath": resource_path
		})
		
		#yield(wait_for_response(1234), 'completed')
		
		pass


func extract_node_path(obj):
	# /root/EditorNode/@@5058/@@4921/@@4922/@@4923/@@4924/@@4925/world/enemies/enemy
	
	var node_path_full = str(obj.get_path())
	var idx_last_internal_node = node_path_full.find_last("@@")
	
	var node_path_scene = node_path_full.right(idx_last_internal_node + 1)
	node_path_scene = node_path_scene.right(node_path_scene.find("/") + 1)	
	
	return node_path_scene
	
func extract_node_path_without_last_node(obj):	
	var node_path = extract_node_path(obj)
	
	return node_path.left(node_path.find_last("/"))
	
func get_scene_node_from_path(node_path):
	if node_path == null:
		muse_log("WARNING: NodePath is null.")
		return null
	
	# The following function will return:
	# /root/EditorNode/@@5058/@@4921/@@4922/@@4923/@@4924/@@4925/world
	
	var root_node: Node = editor_interface.get_edited_scene_root()
	
	if root_node == null:
		muse_log("WARNING: No root scene node found.")
		return null
	
	# We expect: world/enemies/enemy
	# And convert to: enemies/enemy
	
	if node_path.find("/") == -1 and root_node.name == node_path:
		return root_node
	
	var node_path_scene = node_path.right(node_path.find("/") + 1)
	var node = root_node.get_node(node_path_scene)
	
	return node



func muse_send_node_property_update(node):
	var obj_node_path = extract_node_path(node)
	
	var props = [] 
	var node_properties = node.get_property_list()
		
	for property in node_properties:
		var property_value = node.get(property.name)
		
		if (property.name != "translation" 
			and property.name != "rotation"
			and property.name != "scale"
			and property.name != "transform"
			and property.name != "editor/display_folded"
			and property.name != "visible"
			and property.name != "Visibility"):
			continue
		
		props.append({
			"Name": property.name,
			"Data": Marshalls.variant_to_base64(property_value)
		})
		
	muse_send_message("NodeUpdate", {
		"Selector": obj_node_path,
		"Properties": props
	})
	

func muse_undo_redo_callback(obj, operation, arg1 = null, arg2 = null, arg3 = null, arg4 = null):
	# Process only if the client is connected.
	if muse_ws_client == null:
		return
	
	if obj.is_class("EditorInspector"):
		var editor_selection: EditorSelection = editor_interface.get_selection()
		obj = editor_selection.get_selected_nodes()[0]
	
	muse_log_debug("=======================")
	muse_log_debug("muse_undo_redo_callback")
	muse_log_debug(str("obj = ", obj))	
	muse_log_debug(str("obj.get_path() = ", obj.get_path()))
	muse_log_debug(str("obj.get_child_count () = ", obj.get_child_count ()))
	muse_log_debug(str("extract_node_path(obj) = ", extract_node_path(obj)))
	muse_log_debug(str("get_scene_node_from_path(extract_node_path(obj)) = ", get_scene_node_from_path(extract_node_path(obj))))
	muse_log_debug(str("operation = ", operation))
	muse_log_debug(str("arg1 = ", arg1))
	muse_log_debug(str("arg2 = ", arg2))
	muse_log_debug(str("arg3 = ", arg3))
	muse_log_debug(str("arg4 = ", arg4))
	muse_log_debug(">>>>>>>>>>>>>>>>>>>>>>>")
	
	var obj_node_path = extract_node_path(obj)
	
	if (operation == "_edit_request_change"):
		var changed_property = arg2

		if changed_property == "texture":
			var sprite: Sprite = arg1
			
			muse_send_message("ChangeTexture", { 
				"node_path": obj_node_path,
				"resource_path": sprite.texture.resource_path
			})
		else:
			muse_send_node_property_update(obj)
			
			#muse_send_message("ChangeProperty", { 
			#	"node_path": obj_node_path,
			#	"property": changed_property,
			#	"variant_base64": Marshalls.variant_to_base64(obj.get(changed_property))
			#})
		
	if (operation == "set_global_transform"):
		muse_send_node_property_update(obj)
		
	if (operation == "_edit_set_state"):
		var arg1Transform : Transform2D = arg1
		
		muse_send_message("UpdateNodeState", { 
			"node_path": obj_node_path,
			"position": {
				"x": arg1.position.x,
				"y": arg1.position.y
			},
			"transform2d_base64": Marshalls.variant_to_base64(obj.get_transform())
		})
		
	elif (operation == "set_global_position"):
		var new_obj_node_path = extract_node_path(obj)
		var obj_property_list = obj.get_property_list()

		muse_log(str(obj_property_list))
		
		for property in obj_property_list:
			muse_log(str("Property: ", property.name, " = ", obj.get(property.name)))
			if property.name == "texture":
				muse_send_message("ChangeTexture", { 
					"node_path": new_obj_node_path,
					"resource_path": obj.get(property.name).resource_path
				})
			else:
				muse_send_message("ChangeProperty", { 
					"node_path": new_obj_node_path,
					"property": property.name,
					"variant_base64": Marshalls.variant_to_base64(obj.get(property.name))
				})
	elif operation == "add_child_below_node":
		#muse_log(str("obj.get_path()", obj.get_path()))
		#muse_log(str("arg1.get_path()", arg1.get_path()))
		#muse_log(str("arg2.get_path()", arg2.get_path()))
		muse_send_node_add_message(arg2)
	elif (operation == "add_child"):
		muse_send_node_add_message(arg1)
	elif (operation == "remove_child"):
		if ignore_node_on_remove != null and arg1 == ignore_node_on_remove:
			ignore_node_on_remove = null
			return
		
		muse_send_message("NodeRemove", {
			"Selector": obj_node_path + '/' + arg1.name
		})
		
		
	muse_log("<<<<<<<<<<<<<<<<<<<<<<<")

func muse_send_node_add_message(node):
	var node_path = extract_node_path_without_last_node(node);
	
	if node != null and node == ignore_node_on_add:
		ignore_node_on_add = null
		return
	
	var msg = {
		"ParentSelector": node_path,
		"Type": str("Node:", node.get_class()),
		"Name":  node.get_name()
	}
	
	# Handle node properties
	
	var props = []
	
	# Special handlinhg
	
	if (node is MeshInstance):
		var mesh_instance: MeshInstance = node
		var mesh_resource_path = mesh_instance.mesh.resource_path
		
		props.append({
			"Name": "MeshFile",
			"Data": mesh_resource_path
		})
		
	if (node is Spatial):
		var spatial: Spatial = node
		
		if (spatial.filename != ""):
			props.append({
				"Name": "PakedScene",
				"Data": spatial.filename
			})
	
	# Generic properties
	
	var node_properties = node.get_property_list()
		
	for property in node_properties:
		var property_value = node.get(property.name)
		
		var s = str("Property: ", property.name, " = ", property_value)
		
		#node_add_messages.append(muse_prepare_message("NodeAdd", {
		#	"ParentSelector": parent_selector,
		#	"Type": "Property",
		#	"Name": property.name,
		#	"Data": property_value
		#}))
		
		if (property.name != "translation" 
			and property.name != "rotation"
			and property.name != "scale"
			and property.name != "transform"
			and property.name != "editor/display_folded"
			and property.name != "visible"
			and property.name != "Visibility"):
			continue
		
		props.append({
			"Name": property.name,
			"Data": Marshalls.variant_to_base64(property_value)
		})
		
	msg["Properties"] = props
	
	muse_send_message("NodeAdd", msg)

func muse_selection_changed_callback(operation, node):
	if muse_ws_client == null:
		return
		
	muse_log(node)
	
	if operation == "ADD":
		node.locked_by = g_muse_state["username"]
	elif operation == "REMOVE":
		node.locked_by = ""
	
	var obj_node_path = extract_node_path(node)
	var locked_by = node.get("locked_by")
	
	muse_send_message("NodeUpdate", {
		"Selector": obj_node_path,
		"Properties": [
			{
				"Name": "locked_by",
				"Data": Marshalls.variant_to_base64(locked_by)
			}
		]
	})
	
	call_deferred("muse_update_tree_deffered", node)
	
func muse_update_tree_deffered(node):
	if node is Spatial:
		node.update_gizmo()
	
	editor_muse.update_tree()
	
func muse_node_extract_property(node, property):
	return {
		"Name": property,
		"Data": Marshalls.variant_to_base64(node.get(property))
	}
	
func muse_node_update_property_callback(node, property):
	var obj_node_path = extract_node_path(node)
	
	muse_send_message("NodeUpdate", {
		"Selector": obj_node_path,
		"Properties": [
			muse_node_extract_property(node, property)
		]
	})

func muse_event_callback(event, arg1, arg2):
	muse_send_message("NodeReparent", {
		"ParentSelector": extract_node_path(arg2),
		"Selector": extract_node_path(arg1)
	})
	
	ignore_node_on_add = arg1
	ignore_node_on_remove= arg1

func export_tree():
	var scene = get_tree().get_edited_scene_root()
	
	var node_add_messages: Array = []
	
	muse_log(str("Scene filename: ", scene.filename))
	
	var list_nodes: Array = []
	
	list_nodes.append(scene)
	
	while (list_nodes.empty() == false):
		var node = list_nodes.pop_front()
		var node_path = extract_node_path_without_last_node(node);
		
		var clazz = node.get_class()
		var name = node.name
		
		muse_log(str("clazz = ", clazz, " name = ", name))
		
		var parent_selector = node_path
		
		if node == scene:
			parent_selector = ""
			
		var msg = {
			"ParentSelector": parent_selector,
			"Type": str("Node:", clazz),
			"Name": name
		}
		
		# Handle node properties
		
		var props = []
		
		parent_selector = node_path
		var node_properties = node.get_property_list()
		
		# Special handlinhg
		
		if (node is MeshInstance):
			var mesh_instance: MeshInstance = node
			var mesh_resource_path = mesh_instance.mesh.resource_path
			
			props.append({
				"Name": "MeshFile",
				"Data": mesh_resource_path
			})
			
		if (node is Spatial):
			var spatial: Spatial = node
			
			if (spatial.filename != ""):
				props.append({
					"Name": "PakedScene",
					"Data": spatial.filename
				})
		
		# Generic properties
			
		for property in node_properties:
			var property_value = node.get(property.name)
			
			var s = str("Property: ", property.name, " = ", property_value)
			
			if (property.name != "translation" 
				and property.name != "rotation"
				and property.name != "scale"
				and property.name != "transform"
				and property.name != "editor/display_folded"
				and property.name != "visible"
				and property.name != "Visibility"):
				continue
			
			props.append({
				"Name": property.name,
				"Data": Marshalls.variant_to_base64(property_value)
			})
			
		msg["Properties"] = props
			
		
		node_add_messages.append(muse_prepare_message("NodeAdd", msg))
		
		
		# Handle node children
		
		if node != scene and node.filename != "":
			continue
			
		for child in node.get_children():
			list_nodes.append(child)
			
	muse_log(str("JSON = ", JSON.print(node_add_messages)))
	
	return node_add_messages



#################################
# Tests
#
# This section contains a set of test functions for debugging and development.
#
#################################

func _on_debug_test_button_down():
	# _test_file_system_callbacks()
	# _test_traverse_project_file()
	# _test_loading_of_a_mesh()	
	pass
	
func _test_file_system_callbacks():
	var editor_filesystem = editor_interface.get_resource_filesystem() 

	editor_filesystem.connect("filesystem_changed", self, "_test_file_system_callbacks_on_filesystem_changed")
	editor_filesystem.connect("resources_reimported ", self, "_test_file_system_callbacks_on_resources_reimported")
	editor_filesystem.connect("sources_changed", self, "_test_file_system_callbacks_on_sources_changed")	
	
func _test_file_system_callbacks_on_filesystem_changed():
	muse_log_debug('filesystem_changed');

func _test_file_system_callbacks_on_resources_reimported(resources):
	muse_log_debug('resources_reimported')
	
	for e in resources:
		muse_log_debug(e)

func _test_file_system_callbacks_on_sources_changed(exits):
	muse_log_debug('sources_changed');

func _test_traverse_project_file():
	var editor_filesystem = editor_interface.get_resource_filesystem() 
	var editor_filesystem_directory = editor_filesystem.get_filesystem() 

	var directories = [ editor_filesystem_directory ]  

	while directories.empty() == false:
		var directory = directories.pop_front()
	
		muse_log_debug(str("directory = ", directory.get_path()))
		
		# Push all subdirectories.
		
		for idx in range(directory.get_subdir_count()):
			directories.push_front(directory.get_subdir(idx))

		# Go trough all files.

		var file_count = directory.get_file_count()

		for idx in range(file_count):
			var file_path = directory.get_file_path(idx)

			muse_log_debug(file_path)

			var file = File.new()
			file.open(file_path, File.READ)
			
			var md5 = file.get_md5(file_path)
			var bytes = file.get_buffer(file.get_len())			
			var bytes_base64 = Marshalls.raw_to_base64(bytes)
			
			muse_log_debug(str("File-MD5: ", md5))
			muse_log_debug(str("File-length: ", file.get_len()))
			
			if file.get_len() < 1000:
				muse_log_debug(str("File-Base64: ", bytes_base64))	
			
			file.close()

func _test_loading_of_a_mesh():
	var scene = get_tree().get_edited_scene_root()
	
	var newMeshNode = ClassDB.instance("MeshInstance")
	scene.add_child(newMeshNode)
	newMeshNode.set_owner(scene)
	
	newMeshNode.mesh = load("res://Objects/SM_Buildings_Column_2x3_01P.obj")
	
	var newMaterial = ClassDB.instance("SpatialMaterial")
	newMaterial.albedo_color = Color.red
	newMeshNode.set_surface_material(0, newMaterial);
	
	muse_log_debug(newMeshNode.mesh.resource_path)
	