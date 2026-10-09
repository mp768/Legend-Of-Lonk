using Godot;

public partial class GameSignals : Node
{
    public static GameSignals Instance { get; private set; }

    // Godot C# signals are delegates whose names end in "EventHandler". Subscribe with
    // `GameSignals.Instance.X += Handler` (and unsubscribe in _ExitTree), and emit with
    // `EmitSignal(GameSignals.SignalName.X, args...)`.

    [Signal]
    public delegate void CheatModeChangedEventHandler(bool enabled);

    // Both values are in half-hearts.
    [Signal]
    public delegate void PlayerHealthChangedEventHandler(int health, int maxHealth);

    [Signal]
    public delegate void PlayerDiedEventHandler();

    // The player character was grabbed (e.g. by a Wallmaster) and is about to be dragged back to the entrance.
    [Signal]
    public delegate void PlayerGrabbedEventHandler(Node2D player);

    [Signal]
    public delegate void RupeesChangedEventHandler(int rupees);

    [Signal]
    public delegate void KeysChangedEventHandler(int keys);

    [Signal]
    public delegate void BombsChangedEventHandler(int bombs);

    // `kind` is an AltWeapon cast to int, or -1 when no alternate weapon is selected. Godot signals can't
    // carry C# enums directly.
    [Signal]
    public delegate void SelectedWeaponChangedEventHandler(int kind);

    [Signal]
    public delegate void RoomExitEnteredEventHandler(Room destination, Node2D traveller, Vector2 direction);

    // A hard cut to another room, such as stairs, with the traveller placed at `position`.
    [Signal]
    public delegate void WarpRequestedEventHandler(Room destination, Node2D traveller, Vector2 position);

    [Signal]
    public delegate void RoomTransitionStartedEventHandler(Vector2 direction);

    [Signal]
    public delegate void RoomTransitionFinishedEventHandler();

    // `view` is a Room.RoomView cast to int. Emitted after a warp, once the new room is active.
    [Signal]
    public delegate void RoomViewChangedEventHandler(int view);

    [Signal]
    public delegate void DialogueRequestedEventHandler(string text);

    public override void _Ready()
    {
        Instance = this;
    }
}
