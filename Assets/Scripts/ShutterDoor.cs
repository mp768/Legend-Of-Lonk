using Godot;

// A door with no lock. Its just closed.
[GlobalClass]
public partial class ShutterDoor : Door
{
	// Whether the room's clear state drives this shutter (closed while enemies remain). Shutters opened by
    // something else, like a pushed block, turn this off so clearing the room leaves them alone.
    [Export] public bool OpensWhenCleared { get; set; } = true;
}
