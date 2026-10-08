using Godot;

// NES-style walking: one cardinal direction at a time, sliding onto the grid on the other axis so the
// entity lines up with doorways and corridors.
[GlobalClass]
public partial class GridMovementState : MovementState
{
    [Export] public float GridSize { get; set; } = 8f;

    private enum Axis
    {
        NONE,
        HORIZONTAL,
        VERTICAL,
    }

    private Vector2 moveInput;

    // When a diagonal is held, the axis that was pressed first wins unless it's blocked.
    private Axis primaryAxis = Axis.NONE;

    public override void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    public override void Resume()
    {
        Visuals?.FaceWalk(Entity.FacingDirection);
    }

    public override void PhysicsUpdate(double delta)
    {
        float dt = (float)delta;
        Vector2 direction = ResolveDirection(moveInput, dt);
        moveInput = Vector2.Zero;

        if (direction != Vector2.Zero)
        {
            Entity.FacingDirection = direction;
        }

        Entity.Velocity = direction == Vector2.Zero ? Vector2.Zero : GridAlignedVelocity(direction, dt, snap: true);
        Entity.MoveAndSlide();

        Visuals?.StepWalk(direction);
    }

    // Reduces raw input to a single cardinal direction.
    private Vector2 ResolveDirection(Vector2 input, float delta)
    {
        bool hasX = input.X != 0f;
        bool hasY = input.Y != 0f;

        if (!hasX && !hasY)
        {
            primaryAxis = Axis.NONE;
            return Vector2.Zero;
        }

        Vector2 horizontal = new(Mathf.Sign(input.X), 0f);
        Vector2 vertical = new(0f, Mathf.Sign(input.Y));

        if (hasX && hasY)
        {
            if (primaryAxis == Axis.HORIZONTAL)
            {
                return CanMove(horizontal, delta) ? horizontal : vertical;
            }
            if (primaryAxis == Axis.VERTICAL)
            {
                return CanMove(vertical, delta) ? vertical : horizontal;
            }
        }

        if (hasY)
        {
            primaryAxis = Axis.VERTICAL;
            return vertical;
        }

        primaryAxis = Axis.HORIZONTAL;
        return horizontal;
    }

    private bool CanMove(Vector2 direction, float delta)
    {
        Vector2 velocity = GridAlignedVelocity(direction, delta, snap: false);
        return !Entity.TestMove(Entity.GlobalTransform, velocity * delta);
    }

    // Velocity along `direction`, plus a correction on the other axis that pulls the entity onto the grid.
    // With `snap`, a correction smaller than one tick of travel moves the entity straight onto the grid line
    // instead. Collision checks pass false so they don't move anything.
    private Vector2 GridAlignedVelocity(Vector2 direction, float delta, bool snap)
    {
        Vector2 velocity = direction * MoveSpeed;

        // Moving vertically aligns X, and moving horizontally aligns Y.
        int crossAxis = direction.Y != 0f ? (int)Vector2.Axis.X : (int)Vector2.Axis.Y;
        float position = Entity.Position[crossAxis];
        float offset = Mathf.Round(position / GridSize) * GridSize - position;

        if (Mathf.Abs(offset) <= 0.01f)
        {
            return velocity;
        }

        if (Mathf.Abs(offset) > MoveSpeed * delta)
        {
            velocity[crossAxis] = Mathf.Sign(offset) * MoveSpeed;
        }
        else if (snap)
        {
            Vector2 aligned = Entity.Position;
            aligned[crossAxis] += offset;
            Entity.Position = aligned;
        }
        else
        {
            velocity[crossAxis] = offset / delta;
        }

        return velocity;
    }
}
