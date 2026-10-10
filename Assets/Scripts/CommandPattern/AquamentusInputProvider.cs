using Godot;

// Paces back and forth along one axis within a range of where it started, and fires its alternate weapon
// at a fixed interval.
[GlobalClass]
public partial class AquamentusInputProvider : Node, IInputProvider
{
    // The node whose position is paced, normally the entity itself.
    [Export] private Node2D body;

    // How far from its start it may pace, toward PaceDirection.
    [Export] public float PaceRange { get; set; } = 48f;
    [Export] public Vector2 PaceDirection { get; set; } = Vector2.Left;

    [Export] public float FireInterval { get; set; } = 2.5f;

    // Chance out of 1, checked once a second, that it stops pacing for a moment.
    [Export(PropertyHint.Range, "0,1,0.01")] public float PauseChance { get; set; } = 0.3f;
    [Export] public float PauseDuration { get; set; } = 0.75f;

    private static readonly AttackCommand FIRE = new(AttackState.AttackKind.ITEM);

    private Vector2? home;
    private float sign = 1f;
    private float fireTimer;
    private float pauseTimer;
    private float pauseCheckTimer = 1f;

    public ICommand FetchNextCommand()
    {
        float dt = (float)GetPhysicsProcessDeltaTime();

        // The body isn't positioned until it's in the room, so its start is read on first use.
        home ??= body.GlobalPosition;

        fireTimer += dt;
        if (fireTimer >= FireInterval)
        {
            fireTimer = 0f;
            return FIRE;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= dt;
            return new MoveCommand(Vector2.Zero);
        }

        pauseCheckTimer -= dt;
        if (pauseCheckTimer <= 0f)
        {
            pauseCheckTimer = 1f;
            if (GD.Randf() < PauseChance)
            {
                pauseTimer = PauseDuration;
            }
        }

        // Distance travelled from home toward PaceDirection. Turn around at either end of the range.
        Vector2 axis = PaceDirection.Normalized();
        float travelled = (body.GlobalPosition - home.Value).Dot(axis);
        if (travelled >= PaceRange)
        {
            sign = -1f;
        }
        else if (travelled <= 0f)
        {
            sign = 1f;
        }

        return new MoveCommand(axis * sign);
    }
}
