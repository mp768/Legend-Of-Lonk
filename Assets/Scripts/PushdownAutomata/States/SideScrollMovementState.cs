using Godot;

// Movement for side-on rooms like the basement: horizontal input walks, gravity pulls down, and vertical
// input only climbs while the entity is on a ladder (an area on the Ladder layer).
[GlobalClass]
public partial class SideScrollMovementState : MovementState
{
    [Export] public float Gravity { get; set; } = 400f;
    [Export] public float MaxFallSpeed { get; set; } = 160f;
    [Export] public float ClimbSpeed { get; set; } = 50f;

    // Where the ladder check is made, relative to the entity's origin. Defaults to its feet.
    [Export] public Vector2 LadderProbeOffset { get; set; } = new(0, 4);

    private Vector2 moveInput;

    public override void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    public override void Enter()
    {
        // Only left and right make sense side-on, so attacks swing horizontally.
        if (Entity.FacingDirection.X == 0f)
        {
            Entity.FacingDirection = Vector2.Right;
        }
        Entity.Velocity = Vector2.Zero;
        Visuals?.FaceWalk(Entity.FacingDirection);
    }

    public override void Resume()
    {
        Visuals?.FaceWalk(Entity.FacingDirection);
    }

    public override void PhysicsUpdate(double delta)
    {
        float dt = (float)delta;
        Vector2 input = moveInput;
        moveInput = Vector2.Zero;

        float horizontal = Mathf.Sign(input.X);
        float vertical = Mathf.Sign(input.Y);
        Area2D ladder = FindLadder();

        Vector2 velocity = Entity.Velocity;
        velocity.X = horizontal * MoveSpeed;

        if (ladder != null)
        {
            velocity.Y = vertical * ClimbSpeed;

            // Climbing centres the entity on the ladder, so it fits through the shaft.
            if (vertical != 0f)
            {
                velocity.X = 0f;
                Vector2 position = Entity.GlobalPosition;
                position.X = ladder.GlobalPosition.X;
                Entity.GlobalPosition = position;
            }
        }
        else
        {
            velocity.Y = Mathf.Min(velocity.Y + Gravity * dt, MaxFallSpeed);
        }

        if (horizontal != 0f)
        {
            Entity.FacingDirection = new Vector2(horizontal, 0f);
        }
        Entity.MoveIntent = new Vector2(horizontal, ladder != null ? vertical : 0f);

        Entity.Velocity = velocity;
        Entity.UpDirection = Vector2.Up;
        Entity.MoveAndSlide();

        Visuals?.StepWalk(horizontal != 0f ? new Vector2(horizontal, 0f) : Vector2.Zero);
    }

    public override void Exit()
    {
        base.Exit();
        Entity.Velocity = Vector2.Zero;
    }

    private Area2D FindLadder()
    {
        PhysicsPointQueryParameters2D query = new()
        {
            Position = Entity.GlobalPosition + LadderProbeOffset,
            CollideWithAreas = true,
            CollideWithBodies = false,
            CollisionMask = Constants.LADDER_LAYER,
        };

        var hits = Entity.GetWorld2D().DirectSpaceState.IntersectPoint(query, 1);
        return hits.Count > 0 ? hits[0]["collider"].As<Area2D>() : null;
    }
}
