using Godot;

public partial class ResettableEntity : Node2D
{
    public Vector2 initialPosition;

    public override void _Ready()
    {
        initialPosition = GlobalPosition;
    }

    public void OnRoomEntered()
    {
        Visible = true;
        GlobalPosition = initialPosition;

        SetPhysicsProcess(true);
        SetProcess(true);
    }

    public void OnRoomExited()
    {
        SetPhysicsProcess(false);
        SetProcess(false);

        Visible = false;
    }
}