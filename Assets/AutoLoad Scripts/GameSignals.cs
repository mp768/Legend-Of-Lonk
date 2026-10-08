using Godot;

public partial class GameSignals : Node
{
    public static GameSignals Instance { get; private set; }

    // Godot C# signals are delegates whose names end in "EventHandler". Subscribe with
    // `GameSignals.Instance.X += Handler` (and unsubscribe in _ExitTree), and emit with
    // `EmitSignal(GameSignals.SignalName.X, args...)`.

    [Signal]
    public delegate void CheatModeChangedEventHandler(bool enabled);

    [Signal]
    public delegate void PlayerHealthChangedEventHandler(int health);

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void RupeesChangedEventHandler(int rupees);

    [Signal]
    public delegate void KeysChangedEventHandler(int keys);

    [Signal]
    public delegate void RoomExitEnteredEventHandler(Room destination, Node2D traveller, Vector2 direction);

    [Signal]
    public delegate void RoomTransitionStartedEventHandler(Vector2 direction);

    [Signal]
    public delegate void RoomTransitionFinishedEventHandler();

    public override void _Ready()
    {
        Instance = this;
    }
}
