# MUSE - Multi-User-Scene-Editing

The MUSE add-on in general allows multiple users to collaboratively edit scenes by adding or removing nodes or changing node properties within the Godot editor. This repository contains a patch and an add-on for the Godot editor along with a server backend implementation.

## Godot add-on showcase

### 3D scene editing

![alt text][showcase2]

### 2D scene editing (Proof of Concept version)

![alt text][showcase1]

## Features

- project assets synchronization
- scene state synchronization
- highlighting of selected nodes in the scene view and node tree
- visualization of session participants
- locking of nodes

## Requirements

- The MUSE server requires .NET Core 2.2 (download at https://dotnet.microsoft.com/download)

- The 'multi-user-scene-editing' Godot plugin requires Godot 3.1 with applied MUSE patch.

## How to setup MUSE?

1. Build the Godot editor with MUSE extensions (you'll need to setup Godot's tool-chain for building it):

    - Apply the 'godot-muse.patch' to the Godot project and build it. E.g.:
        
        - ```$ git clone https://github.com/godotengine/godot```
        - ```$ cd godot```
        - ```$ git checkout master```
        - ```$ git apply godot-muse.patch```
        - e.g. on Windows: ```$ scons platform=windows```

2. Start the MUSE server.

    - ```$ cd Server && dotnet run```

3. Start everything ...

    - Start the MUSE server and check if it's running on 'http://localhost:5000'
    
    - Copy the 'multi-user-scene-editing' Godot add-on to your Godot project.

    - Open your Godot project and enable the add-on.

    - In the MUSE panel enter the server URL 'ws://127.0.0.1:5000/ws' and your username.

    - Open a scene, press the connect button, invite a teammate, start editing and enjoy collaboration :)

## Known issues:

* The MUSE Godot add-on currently does not properly support synchronization of the following nodes or features:

    * TileMap
    * GridMap
    * Collision Shapes
    * Animations
    * Scripts
	
* Occasional connection problems and crashes :(

## License

This repository is released under MIT license (see LICENSE.txt).

[showcase1]: assets/showcase1-960x540-5fps.gif "2D scene editing showcase"
[showcase2]: assets/showcase2-960x255-5fps.gif "3D scene editing showcase"