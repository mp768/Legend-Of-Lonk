using Godot;

// Plays one dungeon from a list inside the viewport. Pressing "swap_dungeon" moves on to the next dungeon
// with a fresh game.
public partial class GameViewer : Control
{
    [Export] public PackedScene[] Dungeons { get; set; } = [];
    [Export] private SubViewport viewport;

    public override void _Ready()
    {
        if (Dungeons.Length == 0)
        {
            return;
        }

        int index = Mathf.PosMod(GameState.Instance.CurrentDungeon, Dungeons.Length);
        viewport.AddChild(Dungeons[index].Instantiate());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("swap_dungeon") && Dungeons.Length > 1)
        {
            GameState.Instance.CurrentDungeon = (GameState.Instance.CurrentDungeon + 1) % Dungeons.Length;
            GameState.Instance.ResetGame();
        }
    }
}
