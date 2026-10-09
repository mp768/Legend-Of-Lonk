using Godot;

// Scrolls the camera between rooms NES-style when the player walks into an exit, cuts straight to another
// room for warps like stairs, and makes sure only the current room is processing.
public partial class RoomManager : Node
{
    [Export] public Camera2D MainCamera { get; set; }
    [Export] public Room StartingRoom { get; set; }

    // Where the player is dropped in the starting room after being grabbed by a Wallmaster.
    [Export] public Marker2D EntrancePoint { get; set; }

    [ExportGroup("Retro Step Settings")]
    [Export] public int PixelsPerStep { get; set; } = 2;
    [Export] public int FramesBetweenSteps { get; set; } = 2;

    // How long a grabbed player is carried before being dragged back to the entrance.
    [Export] public float GrabWarpDelay { get; set; } = 1f;

    public Room CurrentRoom { get; private set; }
    public bool IsTransitioning { get; private set; }

    public override void _Ready()
    {
        GameSignals.Instance.RoomExitEntered += OnRoomExitEntered;
        GameSignals.Instance.WarpRequested += OnWarpRequested;
        GameSignals.Instance.PlayerGrabbed += OnPlayerGrabbed;

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
        GameSignals.Instance.WarpRequested -= OnWarpRequested;
        GameSignals.Instance.PlayerGrabbed -= OnPlayerGrabbed;
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

    private void OnWarpRequested(Room destination, Node2D traveller, Vector2 position)
    {
        // Warps are requested from inside physics callbacks, where bodies shouldn't be teleported.
        Callable.From(() => Warp(destination, traveller, position)).CallDeferred();
    }

    private async void OnPlayerGrabbed(Node2D player)
    {
        await ToSignal(GetTree().CreateTimer(GrabWarpDelay), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this) || IsQueuedForDeletion() || !IsInsideTree() || !IsInstanceValid(player))
        {
            return;
        }

        Vector2 entrance = EntrancePoint?.GlobalPosition ?? StartingRoom.Center;
        Warp(StartingRoom, player, entrance);
    }

    // A hard cut: no scrolling, the camera and traveller snap straight into the destination.
    private void Warp(Room destination, Node2D traveller, Vector2 position)
    {
        if (IsTransitioning || destination == null || !IsInstanceValid(traveller))
        {
            return;
        }

        IsTransitioning = true;
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.RoomTransitionStarted, Vector2.Zero);

        CurrentRoom.Deactivate();
        CurrentRoom.Visible = false;

        CurrentRoom = destination;
        MainCamera.GlobalPosition = destination.Center;
        traveller.GlobalPosition = position;
        CurrentRoom.Activate();

        IsTransitioning = false;
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.RoomTransitionFinished);

        // Sent last, once the traveller is out of its transition, so it can change how it moves.
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.RoomViewChanged, (int)destination.View);
    }
}
