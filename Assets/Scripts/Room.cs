using Godot;

[GlobalClass]
public partial class Room : Node2D
{
    public Vector2 roomCenter;

    [Export] public Room leftRoom;
    [Export] public Room rightRoom;
    [Export] public Room upRoom;
    [Export] public Room downRoom;

    public override void _Ready()
    {
        // Rooms start frozen and disabled
        SetRoomState(false);

        var leftExit = GetNode<Area2D>("LeftExit");
        var rightExit = GetNode<Area2D>("RightExit");
        var upExit = GetNode<Area2D>("UpExit");
        var downExit = GetNode<Area2D>("DownExit");

        roomCenter = GlobalPosition + Constants.SCREEN_SIZE / 2;

        leftExit.BodyEntered += (body) => RoomManager.Instance.TransitionToRoom(leftRoom, body as CharacterBody2D, roomCenter + new Vector2(-1, 0) * 10.5f * 16f);
        rightExit.BodyEntered += (body) => RoomManager.Instance.TransitionToRoom(rightRoom, body as CharacterBody2D, roomCenter + new Vector2(1, 0) * 10.5f * 16f);
        upExit.BodyEntered += (body) => RoomManager.Instance.TransitionToRoom(upRoom, body as CharacterBody2D, roomCenter + new Vector2(0, -1) * 8f * 16f);
        downExit.BodyEntered += (body) => RoomManager.Instance.TransitionToRoom(downRoom, body as CharacterBody2D, roomCenter + new Vector2(0, 1) * 8f * 16f);
    }

    public void SetRoomState(bool active)
    {
        // ProcessMode = Disabled completely pauses physics, scripts, and animations of all children
        // ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        SetDeferred(Node.PropertyName.ProcessMode, Variant.From(active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled));
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
            if (child is IResettableEntity entity)
            {
                entity.OnRoomEntered();
            }
        }
    }

    private void OnRoomExited()
    {
        foreach (Node child in GetChildren())
        {
            if (child is IResettableEntity entity)
            {
                entity.OnRoomExited();
            }
        }

        // Cleanup or save state (e.g., remove temporary dropped items)
    }
}