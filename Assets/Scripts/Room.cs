using Godot;

// One screen of the dungeon. A room knows its neighbours, reports when the player walks into one of its
// exits, and only processes while it is the active room.
[GlobalClass]
public partial class Room : Node2D
{
    [Export] public Room leftRoom;
    [Export] public Room rightRoom;
    [Export] public Room upRoom;
    [Export] public Room downRoom;

    public Vector2 Center => GlobalPosition + Constants.SCREEN_SIZE / 2;

    public override void _Ready()
    {
        Visible = false;
        SetProcessing(false);

        ConnectExit("LeftExit", leftRoom, Vector2.Left);
        ConnectExit("RightExit", rightRoom, Vector2.Right);
        ConnectExit("UpExit", upRoom, Vector2.Up);
        ConnectExit("DownExit", downRoom, Vector2.Down);
    }

    public void Activate()
    {
        Visible = true;
        SetProcessing(true);

        foreach (Node child in GetChildren())
        {
            if (child is IResettableEntity entity)
            {
                entity.OnRoomEntered();
            }
        }
    }

    // Freezes the room but leaves it visible, so it can scroll off screen during a transition.
    public void Deactivate()
    {
        SetProcessing(false);

        foreach (Node child in GetChildren())
        {
            if (child is IResettableEntity entity)
            {
                entity.OnRoomExited();
            }
        }
    }

    private void SetProcessing(bool enabled)
    {
        // Deferred because rooms switch from inside exit callbacks, while physics is flushing queries.
        // A disabled room pauses physics, scripts and animations for all of its children.
        SetDeferred(Node.PropertyName.ProcessMode, Variant.From(enabled ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled));
    }

    private void ConnectExit(string exitName, Room destination, Vector2 direction)
    {
        GetNode<Area2D>(exitName).BodyEntered += body =>
        {
            if (destination != null && body.IsInGroup(Constants.PLAYER_GROUP))
            {
                GameSignals.Instance.EmitSignal(GameSignals.SignalName.RoomExitEntered, destination, body, direction);
            }
        };
    }
}
