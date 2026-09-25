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
    //
    
    /* CHEAT CODE SIGNALS */
    [Signal]
    public delegate void SetCheatModeEventHandler(bool set); // signal for setting cheat code 

    [Signal]
    public delegate void RupeeUIUpdateEventHandler(int rupee); // signal for updating the rupee count on the UI 

    [Signal]
    public delegate void HealthUIUpdateEventHandler(int health); // signal for updating the health on the UI

    [Signal]
    public delegate void KeysUIUpdateEventHandler(int keys); // signal for updating the key count on the UI


    // FIX THESE SIGNALS BELOW OR REMOVE ENTIRELY

    [Signal]
    public delegate void GameOverEventHandler(); // signal for when game is over

    [Signal]
    public delegate void ScoreUpdateEventHandler(); // signal to update score board
	
    [Signal]
    public delegate void RestartEventHandler(); // signal to restart game

    public override void _Ready()
    {
        Instance = this;
    }
}