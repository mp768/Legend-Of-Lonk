using Godot;

[GlobalClass]
public partial class SideScrollMovementState : MovementState
{
    [Export] public float Gravity { get; set; } = 400f;
    [Export] public float MaxFallSpeed { get; set; } = 160f;
    [Export] public float ClimbSpeed { get; set; } = 50f;

    [Export] public float LandingSnap { get; set; } = 4f;
    [Export] public Vector2 LadderProbeOffset { get; set; } = new(0, 4);

    private Vector2 moveInput;

    public override void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    public override void Enter()
    {
        Entity.Velocity = Vector2.Zero;
        Visuals?.FaceWalk(Entity.FacingDirection);
    }

    public override void PhysicsUpdate(double delta)
    {
        Vector2 walk = new(Mathf.Sign(moveInput.X), 0f);
        Vector2 climb = new(0f, Mathf.Sign(moveInput.Y));
        moveInput = Vector2.Zero;

        Area2D ladder = FindLadder();
        bool onFloor = IsOnFloor();

        bool hanging = ladder != null && !onFloor;
        bool canClimb = ladder != null && (climb.Y < 0f ? FindLadder(Vector2.Up) != null : climb.Y > 0f && !onFloor);

        Vector2 move = Vector2.Zero;
        if (walk != Vector2.Zero && (!hanging || TryLand(ladder, walk)))
        {
            move = walk;
        }
        else if (canClimb)
        {
            move = climb;

            // Have the entity centered on the ladder.
            Entity.GlobalPosition = new Vector2(ladder.GlobalPosition.X, Entity.GlobalPosition.Y);
        }

        // On a ladder either axis turns the entity so the attacks can still be aimed.
        Vector2 facing = move;
        if (facing == Vector2.Zero && hanging)
        {
            facing = climb != Vector2.Zero ? climb : walk;
        }
        if (facing != Vector2.Zero)
        {
            Entity.FacingDirection = facing;
        }
        Entity.MoveIntent = move;

        Vector2 velocity = move * (move == walk ? MoveSpeed : ClimbSpeed);

        // Gravity is enabled only for recovery in case the entity gets knocked into the air, off of the floor and a ladder.
        if (ladder == null && !onFloor)
        {
            velocity.Y = Mathf.Min(Entity.Velocity.Y + Gravity * (float)delta, MaxFallSpeed);
        }

        Entity.Velocity = velocity;
        Entity.UpDirection = Vector2.Up;
        Entity.MoveAndSlide();

        Visuals?.FaceWalk(Entity.FacingDirection);
        Visuals?.StepWalk(move);
    }

    public override void Exit()
    {
        base.Exit();
        Entity.Velocity = Vector2.Zero;
    }

    private bool TryLand(Area2D ladder, Vector2 walk)
    {
        Transform2D onLadder = Entity.GlobalTransform;
        onLadder.Origin = new Vector2(ladder.GlobalPosition.X, onLadder.Origin.Y);

        Vector2 sideStep = walk * Constants.TILE_SIZE / 2f;
        if (Entity.TestMove(onLadder, sideStep))
        {
            return false;
        }

        var floor = new KinematicCollision2D();
        if (!Entity.TestMove(onLadder.Translated(sideStep), Vector2.Down * LandingSnap, floor))
        {
            return false;
        }

        Entity.GlobalPosition += floor.GetTravel();
        return true;
    }

    private bool IsOnFloor() => Entity.TestMove(Entity.GlobalTransform, Vector2.Down);

    private Area2D FindLadder(Vector2 probeShift = default)
    {
        PhysicsPointQueryParameters2D query = new()
        {
            Position = Entity.GlobalPosition + LadderProbeOffset + probeShift,
            CollideWithAreas = true,
            CollideWithBodies = false,
            CollisionMask = Constants.LADDER_LAYER,
        };

        var hits = Entity.GetWorld2D().DirectSpaceState.IntersectPoint(query, 1);
        return hits.Count > 0 ? hits[0]["collider"].As<Area2D>() : null;
    }
}
