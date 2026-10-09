using Godot;

// Charges in a straight line until it hits something or has gone far enough, then slides slowly back to
// where it started and pops. Used by blade traps. It can't be interrupted.
[GlobalClass]
public partial class LungeState : State
{
    [Export] public float LungeSpeed { get; set; } = 180f;
    [Export] public float ReturnSpeed { get; set; } = 50f;

    // How far a lunge may travel along each axis before turning back. Rooms are wider than they are tall.
    [Export] public float MaxDistanceHorizontal { get; set; } = 88f;
    [Export] public float MaxDistanceVertical { get; set; } = 48f;

    public override bool IsInterruptable => false;

    private Vector2 direction;
    private Vector2 home;
    private bool returning;

    public void Configure(Vector2 lungeDirection)
    {
        direction = lungeDirection.Normalized();
    }

    public override void Enter()
    {
        home = Entity.GlobalPosition;
        returning = false;
        Entity.FacingDirection = direction;
    }

    public override void PhysicsUpdate(double delta)
    {
        float dt = (float)delta;

        if (!returning)
        {
            float maxDistance = direction.X != 0f ? MaxDistanceHorizontal : MaxDistanceVertical;
            KinematicCollision2D collision = Entity.MoveAndCollide(direction * LungeSpeed * dt);
            if (collision != null || Entity.GlobalPosition.DistanceTo(home) >= maxDistance)
            {
                returning = true;
            }
            return;
        }

        Entity.GlobalPosition = Entity.GlobalPosition.MoveToward(home, ReturnSpeed * dt);
        if (Entity.GlobalPosition.IsEqualApprox(home))
        {
            Machine.PopState();
        }
    }
}
