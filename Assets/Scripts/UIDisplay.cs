using Godot;

public partial class UIDisplay : ColorRect
{
    private Label health;
    private Label rupees;
    private Label keys;
    private Label bombs;
    private TextureRect weaponIcon;

    public override void _Ready()
    {
        health = GetNode<Label>("%Health");
        rupees = GetNode<Label>("%Rupees");
        keys = GetNode<Label>("%Keys");
        bombs = GetNode<Label>("%Bombs");
        weaponIcon = GetNode<TextureRect>("%Weapon");

        GameSignals.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
        GameSignals.Instance.RupeesChanged += OnRupeesChanged;
        GameSignals.Instance.KeysChanged += OnKeysChanged;
        GameSignals.Instance.BombsChanged += OnBombsChanged;
        GameSignals.Instance.PlayerWeaponChanged += OnPlayerWeaponChanged;

        // GameState survives scene reloads, so read its current values instead of waiting for a change.
        // Health and the alternate weapon come from the player's body when it spawns or takes over.
        OnRupeesChanged(GameState.Instance.Rupees);
        OnKeysChanged(GameState.Instance.Keys);
        OnBombsChanged(GameState.Instance.Bombs);
    }

    public override void _ExitTree()
    {
        GameSignals.Instance.PlayerHealthChanged -= OnPlayerHealthChanged;
        GameSignals.Instance.RupeesChanged -= OnRupeesChanged;
        GameSignals.Instance.KeysChanged -= OnKeysChanged;
        GameSignals.Instance.BombsChanged -= OnBombsChanged;
        GameSignals.Instance.PlayerWeaponChanged -= OnPlayerWeaponChanged;
    }

    // Health is in half-hearts, shown as hearts.
    private void OnPlayerHealthChanged(int current, int max)
    {
        health.Text = $"Hearts: {current / 2f:0.#}/{max / 2f:0.#}";
    }

    private void OnRupeesChanged(int value) => ShowStat(rupees, value);

    private void OnKeysChanged(int value) => ShowStat(keys, value);

    private void OnBombsChanged(int value) => ShowStat(bombs, value);

    private void OnPlayerWeaponChanged(Weapon weapon)
    {
        weaponIcon.Texture = weapon?.Icon;
        weaponIcon.TooltipText = weapon?.DisplayName ?? "";
    }

    private static void ShowStat(Label label, int value)
    {
        label.Text = $"{label.Name}: {value}";
    }
}
