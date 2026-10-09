using Godot;

// Wanders in a random cardinal direction and picks a new one at a fixed interval. With a pause chance it
// sometimes stops for a moment instead, which gives hop-and-stop movers like Gels.
[GlobalClass]
public partial class RandomWalkInputProvider : Node, IInputProvider
{
    private static readonly MoveCommand[] CARDINAL_MOVES =
    [
        new(Vector2.Left),
        new(Vector2.Right),
        new(Vector2.Down),
        new(Vector2.Up),
    ];

    private static readonly MoveCommand IDLE = new(Vector2.Zero);

    [Export] public float DirectionChangeInterval { get; set; } = 0.78f;

    // Chance out of 1 that each new pick is a pause rather than a direction.
    [Export(PropertyHint.Range, "0,1,0.01")] public float PauseChance { get; set; }
    [Export] public float PauseDuration { get; set; } = 0.5f;

    private MoveCommand currentMove = CARDINAL_MOVES[0];
    private Timer timer;

    public override void _Ready()
    {
        timer = new Timer { OneShot = true };
        timer.Timeout += PickDirection;
        AddChild(timer);
        timer.Start(DirectionChangeInterval);
    }

    public virtual ICommand FetchNextCommand()
    {
        return currentMove;
    }

    private void PickDirection()
    {
        if (GD.Randf() < PauseChance)
        {
            currentMove = IDLE;
            timer.Start(PauseDuration);
            return;
        }

        currentMove = CARDINAL_MOVES[GD.RandRange(0, CARDINAL_MOVES.Length - 1)];
        timer.Start(DirectionChangeInterval);
    }
}
