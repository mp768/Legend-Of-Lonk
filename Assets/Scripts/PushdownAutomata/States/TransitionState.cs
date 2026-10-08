using Godot;

// Plays the walk cycle while a room transition carries the entity into the next room. It ignores input
// and can't be interrupted.
[GlobalClass]
public partial class TransitionState : State
{
    public override bool IsInterruptable => false;

    private Vector2 walkDirection;

    public void Configure(Vector2 direction)
    {
        walkDirection = direction;
    }

    public override void Enter()
    {
        Entity.FacingDirection = walkDirection;
    }

    public override void PhysicsUpdate(double delta)
    {
        Visuals?.StepWalk(walkDirection);
    }
}
