using Godot;

public partial class UIDisplay : ColorRect
{
    private Label health;
    private Label rupees;
    private Label keys;

    public override void _Ready()
    {
        health = GetNode<Label>("%Health");
        rupees = GetNode<Label>("%Rupees");
        keys = GetNode<Label>("%Keys");

        GameSignals.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
        GameSignals.Instance.RupeesChanged += OnRupeesChanged;
        GameSignals.Instance.KeysChanged += OnKeysChanged;

        // GameState survives scene reloads, so read its current values instead of waiting for a change.
        // Health comes from the player when he spawns.
        OnRupeesChanged(GameState.Instance.Rupees);
        OnKeysChanged(GameState.Instance.Keys);
    }

    public override void _ExitTree()
    {
        GameSignals.Instance.PlayerHealthChanged -= OnPlayerHealthChanged;
        GameSignals.Instance.RupeesChanged -= OnRupeesChanged;
        GameSignals.Instance.KeysChanged -= OnKeysChanged;
    }

    private void OnPlayerHealthChanged(int value) => ShowStat(health, value);

    private void OnRupeesChanged(int value) => ShowStat(rupees, value);

    private void OnKeysChanged(int value) => ShowStat(keys, value);

    private static void ShowStat(Label label, int value)
    {
        label.Text = $"{label.Name}: {value}";
    }
}
