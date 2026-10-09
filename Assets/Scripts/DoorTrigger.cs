using Godot;

// Opens a set of doors when fired. Anything that should open a door connects a signal to Fire, such as a
// pushable block's Pushed signal.
[GlobalClass]
public partial class DoorTrigger : Node
{
    [Export] public Door[] Targets { get; set; } = [];

    public void Fire()
    {
        foreach (Door door in Targets)
        {
            door?.Open();
        }
    }
}
