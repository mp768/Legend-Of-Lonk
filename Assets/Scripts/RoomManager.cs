using Godot;

// Scrolls the camera between rooms NES-style when the player walks into an exit, and makes sure only the
// current room is processing.
public partial class RoomManager : Node
{
    [Export] public Camera2D MainCamera { get; set; }
    [Export] public Room StartingRoom { get; set; }

    [ExportGroup("Retro Step Settings")]
    [Export] public int PixelsPerStep { get; set; } = 2;
    [Export] public int FramesBetweenSteps { get; set; } = 2;

    public Room CurrentRoom { get; private set; }
    public bool IsTransitioning { get; private set; }

    public override void _Ready()
    {
        GameSignals.Instance.RoomExitEntered += OnRoomExitEntered;

        CurrentRoom = StartingRoom;
        if (CurrentRoom == null)
        {
            return;
        }

        CurrentRoom.Activate();
        if (MainCamera != null)
        {
            MainCamera.GlobalPosition = CurrentRoom.Center;
        }
    }

    public override void _ExitTree()
    {
        GameSignals.Instance.RoomExitEntered -= OnRoomExitEntered;
    }

    private async void OnRoomExitEntered(Room destination, Node2D traveller, Vector2 direction)
    {
        // A room can list itself as a neighbour to block an exit.
        if (IsTransitioning || destination == null || destination == CurrentRoom)
        {
            return;
        }

        IsTransitioning = true;

        CurrentRoom.Deactivate();
        destination.Visible = true;
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.RoomTransitionStarted, direction);

        Vector2 halfRoom = Constants.SCREEN_SIZE / 2;
        Vector2 cameraStart = MainCamera.GlobalPosition;
        Vector2 cameraTarget = destination.Center;
        Vector2 travellerStart = traveller.GlobalPosition;
        Vector2 travellerTarget = destination.Center - direction * (halfRoom - Vector2.One * Constants.ROOM_ENTRY_INSET);

        // The camera moves a whole number of pixels per step, and the traveller keeps pace with it.
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(cameraStart.DistanceTo(cameraTarget) / PixelsPerStep));
        for (int step = 1; step <= stepCount; step++)
        {
            MainCamera.GlobalPosition = cameraStart.MoveToward(cameraTarget, step * PixelsPerStep);
            traveller.GlobalPosition = travellerStart.Lerp(travellerTarget, (float)step / stepCount);

            for (int frame = 0; frame < FramesBetweenSteps; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                // The scene may have been reloaded while we were waiting.
                if (!IsInstanceValid(this) || IsQueuedForDeletion() || !IsInsideTree() || !IsInstanceValid(traveller))
                {
                    return;
                }
            }
        }

        MainCamera.GlobalPosition = cameraTarget;
        traveller.GlobalPosition = travellerTarget;

        CurrentRoom.Visible = false;
        CurrentRoom = destination;
        CurrentRoom.Activate();

        IsTransitioning = false;
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.RoomTransitionFinished);
    }
}
