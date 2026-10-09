using Godot;

// A door with no lock. Its room closes it while enemies remain (when the room locks until cleared), and
// a DoorTrigger can open it, e.g. when a block is pushed.
[GlobalClass]
public partial class ShutterDoor : Door
{
}
