using Godot;
using System;
using System.Collections.Generic;

public partial class GameState : Node
{
    public static GameState Instance { get; private set; }

    private int rupees;
    private int keys;
    private int bombs;
    private AltWeapon? selectedWeapon;

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

    public int Bombs
    {
        get => bombs;
        private set
        {
            bombs = Mathf.Clamp(value, 0, Constants.MAX_BOMBS);
            GameSignals.Instance.EmitSignal(GameSignals.SignalName.BombsChanged, bombs);
        }
    }

    // The alternate weapon the player character fires, or null before any has been picked up.
    public AltWeapon? SelectedWeapon
    {
        get => selectedWeapon;
        private set
        {
            selectedWeapon = value;
            GameSignals.Instance.EmitSignal(GameSignals.SignalName.SelectedWeaponChanged, value.HasValue ? (int)value.Value : -1);
        }
    }

    public IReadOnlySet<AltWeapon> UnlockedWeapons => unlockedWeapons;

    // While enabled, rupees, keys and bombs sit at the cap and spending them costs nothing.
    public bool CheatsEnabled { get; private set; }

    private readonly HashSet<AltWeapon> unlockedWeapons = [];

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

    public void AddBombs(int amount)
    {
        Bombs += amount;
    }

    // Returns false, and spends nothing, if there aren't enough keys.
    public bool TryUseKeys(int amount)
    {
        if (!CanAfford(Keys, amount))
        {
            return false;
        }

        if (!CheatsEnabled)
        {
            Keys -= amount;
        }
        return true;
    }

    public bool TrySpendRupees(int amount)
    {
        if (!CanAfford(Rupees, amount))
        {
            return false;
        }

        if (!CheatsEnabled)
        {
            Rupees -= amount;
        }
        return true;
    }

    public bool TrySpendBombs(int amount)
    {
        if (!CanAfford(Bombs, amount))
        {
            return false;
        }

        if (!CheatsEnabled)
        {
            Bombs -= amount;
        }
        return true;
    }

    // Unlocking the first weapon also selects it, so the player can use it straight away.
    public void UnlockWeapon(AltWeapon weapon)
    {
        if (unlockedWeapons.Add(weapon) && SelectedWeapon == null)
        {
            SelectedWeapon = weapon;
        }
    }

    // Steps to the next unlocked weapon in AltWeapon order, wrapping around. Bombs are skipped while
    // there are none to throw.
    public void CycleWeapon()
    {
        AltWeapon[] all = Enum.GetValues<AltWeapon>();
        int start = SelectedWeapon.HasValue ? Array.IndexOf(all, SelectedWeapon.Value) : -1;

        for (int step = 1; step <= all.Length; step++)
        {
            AltWeapon candidate = all[(start + step + all.Length) % all.Length];
            if (IsSelectable(candidate))
            {
                if (candidate != SelectedWeapon)
                {
                    SelectedWeapon = candidate;
                }
                return;
            }
        }
    }

    public void ToggleCheatMode()
    {
        CheatsEnabled = !CheatsEnabled;

        // Turning cheats off leaves the maxed counts and unlocked weapons in place.
        if (CheatsEnabled)
        {
            Rupees = Constants.MAX_COLLECTABLE;
            Keys = Constants.MAX_COLLECTABLE;
            Bombs = Constants.MAX_BOMBS;

            foreach (AltWeapon weapon in Enum.GetValues<AltWeapon>())
            {
                UnlockWeapon(weapon);
            }
        }

        GameSignals.Instance.EmitSignal(GameSignals.SignalName.CheatModeChanged, CheatsEnabled);
    }

    public void ResetGame()
    {
        CheatsEnabled = false;
        Rupees = 0;
        Keys = 0;
        Bombs = 0;
        unlockedWeapons.Clear();
        SelectedWeapon = null;

        Error error = GetTree().ReloadCurrentScene();
        if (error != Error.Ok)
        {
            GD.PushError($"Failed to reload the current scene: {error}");
        }
    }

    private bool CanAfford(int owned, int cost) => CheatsEnabled || owned >= cost;

    private bool IsSelectable(AltWeapon weapon)
    {
        if (!unlockedWeapons.Contains(weapon))
        {
            return false;
        }

        return weapon != AltWeapon.BOMB || Bombs > 0 || CheatsEnabled;
    }

    private void OnPlayerDied()
    {
        // Death is reported from inside a physics callback, where the scene can't be torn down safely.
        CallDeferred(MethodName.ResetGame);
    }
}
