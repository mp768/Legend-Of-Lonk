using Godot;

// Wanders in a random cardinal direction and picks a new one at a fixed interval.
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

    [Export] public float DirectionChangeInterval { get; set; } = 0.78f;

    private MoveCommand currentMove = CARDINAL_MOVES[0];

    public override void _Ready()
    {
        var timer = new Timer
        {
            WaitTime = DirectionChangeInterval,
            Autostart = true,
        };
        timer.Timeout += PickDirection;
        AddChild(timer);
    }

    public ICommand FetchNextCommand()
    {
        return currentMove;
    }

    private void PickDirection()
    {
        currentMove = CARDINAL_MOVES[GD.RandRange(0, CARDINAL_MOVES.Length - 1)];
    }
}
