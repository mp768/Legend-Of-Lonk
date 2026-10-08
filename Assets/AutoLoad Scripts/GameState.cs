using Godot;

public partial class GameState : Node
{
    public static GameState Instance { get; private set; }

    private int rupees;
    private int keys;

    public int Rupees
    {
        get => rupees;
        private set
        {
            rupees = Mathf.Clamp(value, 0, Constants.MAX_COLLECTABLE);
            GameSignals.Instance.EmitSignal(GameSignals.SignalName.RupeesChanged, rupees);
        }
    }

    public int Keys
    {
        get => keys;
        private set
        {
            keys = Mathf.Clamp(value, 0, Constants.MAX_COLLECTABLE);
            GameSignals.Instance.EmitSignal(GameSignals.SignalName.KeysChanged, keys);
        }
    }

    // While enabled, rupees and keys sit at the cap and spending them costs nothing.
    public bool CheatsEnabled { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        GameSignals.Instance.PlayerDied += OnPlayerDied;
    }

    public override void _ExitTree()
    {
        GameSignals.Instance.PlayerDied -= OnPlayerDied;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("cheat_mode"))
        {
            ToggleCheatMode();
        }
    }

    public void AddRupees(int amount)
    {
        Rupees += amount;
    }

    public void AddKeys(int amount)
    {
        Keys += amount;
    }

    // Returns false, and spends nothing, if there aren't enough keys.
    public bool TryUseKeys(int amount)
    {
        if (CheatsEnabled)
        {
            return true;
        }

        if (Keys < amount)
        {
            return false;
        }

        Keys -= amount;
        return true;
    }

    public void ToggleCheatMode()
    {
        CheatsEnabled = !CheatsEnabled;

        // Turning cheats off leaves the maxed counts in place.
        if (CheatsEnabled)
        {
            Rupees = Constants.MAX_COLLECTABLE;
            Keys = Constants.MAX_COLLECTABLE;
        }

        GameSignals.Instance.EmitSignal(GameSignals.SignalName.CheatModeChanged, CheatsEnabled);
    }

    public void ResetGame()
    {
        CheatsEnabled = false;
        Rupees = 0;
        Keys = 0;

        Error error = GetTree().ReloadCurrentScene();
        if (error != Error.Ok)
        {
            GD.PushError($"Failed to reload the current scene: {error}");
        }
    }

    private void OnPlayerDied()
    {
        // Death is reported from inside a physics callback, where the scene can't be torn down safely.
        CallDeferred(MethodName.ResetGame);
    }
}
