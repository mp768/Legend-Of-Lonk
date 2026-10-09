using Godot;

// Unrestricted movement in any direction with no grid snapping, for flyers and floaters like Keese. The
// length of the input (up to 1) scales the speed, so a controller can speed up and slow down smoothly.
[GlobalClass]
public partial class FreeMovementState : MovementState
{
    private Vector2 moveInput;

    public override void SetMoveInput(Vector2 input)
    {
        moveInput = input.LimitLength(1f);
    }

    public override void Resume()
    {
        Visuals?.FaceWalk(Entity.FacingDirection);
    }

    public override void PhysicsUpdate(double delta)
    {
        Vector2 input = moveInput;
        moveInput = Vector2.Zero;

        Vector2 cardinal = DominantAxis(input);
        if (cardinal != Vector2.Zero)
        {
            Entity.FacingDirection = cardinal;
        }
        Entity.MoveIntent = cardinal;

        Entity.Velocity = input * MoveSpeed;
        Entity.MoveAndSlide();

        Visuals?.StepWalk(cardinal);
    }

    // Facing is always cardinal, since attacks and poses only come in four directions.
    private static Vector2 DominantAxis(Vector2 input)
    {
        if (input == Vector2.Zero)
        {
            return Vector2.Zero;
        }

        return Mathf.Abs(input.X) >= Mathf.Abs(input.Y)
            ? new Vector2(Mathf.Sign(input.X), 0f)
            : new Vector2(0f, Mathf.Sign(input.Y));
    }
}
