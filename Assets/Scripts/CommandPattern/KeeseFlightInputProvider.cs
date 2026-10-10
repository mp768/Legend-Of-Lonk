using Godot;

// Keese-style flight
[GlobalClass]
public partial class KeeseFlightInputProvider : Node, IInputProvider
{
    private static readonly Vector2[] EIGHT_DIRECTIONS =
    [
        Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down,
        new Vector2(1, 1).Normalized(), new Vector2(1, -1).Normalized(),
        new Vector2(-1, 1).Normalized(), new Vector2(-1, -1).Normalized(),
    ];

    [Export] public float MinFlightDuration { get; set; } = 2f;
    [Export] public float MaxFlightDuration { get; set; } = 4f;
    [Export] public float MinPerchDuration { get; set; } = 0.5f;
    [Export] public float MaxPerchDuration { get; set; } = 1.5f;
    [Export] public float MinTurnInterval { get; set; } = 0.2f;
    [Export] public float MaxTurnInterval { get; set; } = 0.6f;

    private bool perched = true;
    private float phaseTimer;
    private float phaseDuration;
    private float turnTimer;
    private Vector2 heading = Vector2.Right;

    public ICommand FetchNextCommand()
    {
        float dt = (float)GetPhysicsProcessDeltaTime();
        phaseTimer -= dt;

        if (phaseTimer <= 0f)
        {
            perched = !perched;
            phaseDuration = perched
                ? (float)GD.RandRange(MinPerchDuration, MaxPerchDuration)
                : (float)GD.RandRange(MinFlightDuration, MaxFlightDuration);
            phaseTimer = phaseDuration;
        }

        if (perched)
        {
            return new MoveCommand(Vector2.Zero);
        }

        turnTimer -= dt;
        if (turnTimer <= 0f)
        {
            heading = EIGHT_DIRECTIONS[GD.RandRange(0, EIGHT_DIRECTIONS.Length - 1)];
            turnTimer = (float)GD.RandRange(MinTurnInterval, MaxTurnInterval);
        }

        // Ramps up from a standstill and back down to land, peaking halfway through the flight.
        float progress = 1f - phaseTimer / phaseDuration;
        float speed = Mathf.Sin(progress * Mathf.Pi);
        return new MoveCommand(heading * speed);
    }
}
