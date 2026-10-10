using Godot;

public partial class GameSignals : Node
{
    public static GameSignals Instance { get; private set; }

    // NOTE: Godot has this concept of a "signal", which is essentially just a callback. For the C# version of
    // Godot, you need to have the "EventHandler" suffix for any "signal" you want to declare. 
    //
    // When accessing the signal, declared as "XYZEventHandler" for example, you would use "SignalName.XYZ" to 
    // get it's string name. To then "listen" to it and "invoke" the callback, you would use either 
    // 
    // "XYZ += MethodName" to set up the listener and "XYZ -= MethodName" to disconnect it
    // or 
    // "EmitSignal(SignalName.XYZ, ..any args go here)" to invoke the callback, calling all connected listeners.

    [Signal]
    public delegate void CheatModeChangedEventHandler(bool enabled);

    // Both values are in half-hearts.
    [Signal]
    public delegate void PlayerHealthChangedEventHandler(int health, int maxHealth);

    [Signal]
    public delegate void PlayerDiedEventHandler();

    // The player character was grabbed and is about to be dragged back to the entrance.
    [Signal]
    public delegate void PlayerGrabbedEventHandler(Node2D player);

    [Signal]
    public delegate void RupeesChangedEventHandler(int rupees);

    [Signal]
    public delegate void KeysChangedEventHandler(int keys);

    [Signal]
    public delegate void BombsChangedEventHandler(int bombs);

    // `kind` is an `AltWeapon` cast to int, or -1 when no alternate weapon is selected. 
    // NOTE: This is because Godot signals can't use C# enums directly.
    [Signal]
    public delegate void SelectedWeaponChangedEventHandler(int kind);

    [Signal]
    public delegate void RoomExitEnteredEventHandler(Room destination, Node2D traveler, Vector2 direction);

    // A hard cut to another room, such as stairs, with the `traveler` placed at `position`.
    [Signal]
    public delegate void WarpRequestedEventHandler(Room destination, Node2D traveler, Vector2 position);

    [Signal]
    public delegate void RoomTransitionStartedEventHandler(Vector2 direction);

    [Signal]
    public delegate void RoomTransitionFinishedEventHandler();

    // `view` is a Room.RoomView cast to int, used to tell us what kind of room we're entering (side-scrolling or top-down). 
    // Emitted specifically after a warp, once the new room is active.
    [Signal]
    public delegate void RoomViewChangedEventHandler(int view);

    [Signal]
    public delegate void DialogueRequestedEventHandler(string text);

    public override void _Ready()
    {
        Instance = this;
    }
}
