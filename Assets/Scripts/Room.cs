using Godot;

[GlobalClass]
public partial class Room : Node2D
{
    [Export] public Marker2D cameraCenterMarker;

    public override void _Ready()
    {
        // Rooms start frozen and disabled
        SetRoomState(false);
    }

    public void SetRoomState(bool active)
    {
        // ProcessMode = Disabled completely pauses physics, scripts, and animations of all children
        ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        Visible = active;

        if (active)
        {
            OnRoomEntered();
        }
        else
        {
            OnRoomExited();
        }
    }

    private void OnRoomEntered()
    {
        // Spawning Logic: Instantiate enemies here if they don't exist yet,
        // or trigger custom activation hooks on existing pre-placed enemies.
        foreach (Node child in GetChildren())
        {
            if (child is ResettableEntity entity)
            {
                entity.OnRoomEntered();
            }
        }
    }

    private void OnRoomExited()
    {
        foreach (Node child in GetChildren())
        {
            if (child is ResettableEntity entity)
            {
                entity.OnRoomExited();
            }
        }

        // Cleanup or save state (e.g., remove temporary dropped items)
    }
}