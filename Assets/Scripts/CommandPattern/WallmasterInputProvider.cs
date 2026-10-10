using Godot;

// An input provider that simply moves outward, along, and backward on an L-shaped path.
[GlobalClass]
public partial class WallmasterInputProvider : Node, IInputProvider
{
    [Signal]
    public delegate void PathFinishedEventHandler();

    // The node whose position is tracked along the path, normally the entity itself.
    [Export] private Node2D body;

    private Vector2[] legDirections = [];
    private float[] legLengths = [];
    private int leg;
    private Vector2 legStart;
    private bool started;
    private bool finished;

    // `outward` points from the wall into the room, and `along` runs parallel to the wall.
    public void Configure(Vector2 outward, float outDistance, Vector2 along, float alongDistance)
    {
        legDirections = [outward, along, -outward];
        legLengths = [outDistance, alongDistance, outDistance];
        leg = 0;
        started = false;
        finished = false;
    }

    public ICommand FetchNextCommand()
    {
        if (finished || legDirections.Length == 0)
        {
            return null;
        }

        if (!started)
        {
            started = true;
            legStart = body.GlobalPosition;
        }

        if (body.GlobalPosition.DistanceTo(legStart) >= legLengths[leg])
        {
            leg++;
            legStart = body.GlobalPosition;

            if (leg >= legDirections.Length)
            {
                finished = true;
                EmitSignal(SignalName.PathFinished);
                return new MoveCommand(Vector2.Zero);
            }
        }

        return new MoveCommand(legDirections[leg]);
    }
}
