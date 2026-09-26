using Godot;
using System.Threading.Tasks;

public partial class RoomManager : Node
{
    public static RoomManager Instance { get; private set; }

    [Export] public Camera2D MainCamera { get; set; }
    [Export] public Room StartingRoom { get; set; }

    [ExportGroup("Retro Step Settings")]
    [Export] public int PixelsPerStep { get; set; } = 2;       // Distance moved per step
    [Export] public int FramesBetweenSteps { get; set; } = 2;  // Frame delay between steps

    public Room CurrentRoom { get; private set; }
    public bool IsTransitioning { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        if (StartingRoom != null)
        {
            CurrentRoom = StartingRoom;
            CurrentRoom.SetRoomState(true);
            if (MainCamera != null)
            {
                MainCamera.GlobalPosition = StartingRoom.roomCenter;
            }
        }
    }

    public async void TransitionToRoom(Room nextRoom, CharacterBody2D player, Vector2 playerTargetPos)
    {
        // Guard clause: ignore if already transitioning or entering the current room
        if (IsTransitioning || nextRoom == CurrentRoom || nextRoom == null) return;

        IsTransitioning = true;

        // Lock player physics and manual inputs
        player.SetPhysicsProcess(false);

        // Make target and current room visible (processing remains disabled for now)
        nextRoom.Visible = true;
        CurrentRoom.SetRoomState(false);
        CurrentRoom.Visible = true;

        Vector2 cameraTargetPos = nextRoom.roomCenter;

        Vector2 startCamPos = MainCamera.GlobalPosition;
        Vector2 startPlayerPos = player.GlobalPosition;

        Vector2 camDelta = cameraTargetPos - startCamPos;
        Vector2 playerDelta = playerTargetPos - startPlayerPos;

        float totalCamDistance = camDelta.Length();
        Vector2 camDir = totalCamDistance > 0 ? camDelta.Normalized() : Vector2.Zero;
        Vector2 playerDir = playerDelta.Length() > 0 ? playerDelta.Normalized() : Vector2.Zero;

        // Calculate discrete frame steps
        int totalSteps = Mathf.Max(1, Mathf.CeilToInt(totalCamDistance / PixelsPerStep));
        Vector2 playerStepIncrement = playerDelta / totalSteps;

        // Get Link's animation component (if attached as a child node or via player)
        var animComponent = player.GetNodeOrNull<GridAnimationComponent>("GridAnimationComponent");

        // --- COROUTINE STEPPING LOOP ---
        for (int step = 0; step < totalSteps; step++)
        {
            // 1. Advance camera by fixed pixel step
            Vector2 nextCamPos = MainCamera.GlobalPosition + (camDir * PixelsPerStep);
            if ((nextCamPos - startCamPos).Length() > totalCamDistance)
            {
                nextCamPos = cameraTargetPos;
            }
            MainCamera.GlobalPosition = nextCamPos;

            // 2. Advance player position and drive step-animation
            player.GlobalPosition += playerStepIncrement;
            animComponent?.UpdateAnimation(playerDir);

            // 3. Yield execution frame-by-frame
            for (int f = 0; f < FramesBetweenSteps; f++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        // Hard snap positions at the end to correct subpixel float drift
        MainCamera.GlobalPosition = cameraTargetPos;
        player.GlobalPosition = playerTargetPos;

        // Stop movement frame counter on animation component
        animComponent?.UpdateAnimation(Vector2.Zero);

        // Swap room states
        if (CurrentRoom != null)
        {
            CurrentRoom.SetRoomState(false);
        }

        CurrentRoom = nextRoom;
        CurrentRoom.SetRoomState(true);

        // Restore player control
        player.SetPhysicsProcess(true);
        IsTransitioning = false;
    }
}