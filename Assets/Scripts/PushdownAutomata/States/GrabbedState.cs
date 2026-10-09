using Godot;

// Held helpless and carried along with whatever grabbed the entity, e.g. a Wallmaster. It ignores input
// and can't be interrupted. The owner pops it when the grab resolves, or it times out on its own.
[GlobalClass]
public partial class GrabbedState : State
{
    // Safety net in case nothing ends the grab. 0 waits forever.
    [Export] public float MaxDuration { get; set; } = 3f;

    [Export] public string PoseAnimation { get; set; } = "walk_down";

    public override bool IsInterruptable => false;

    private Node2D carrier;
    private float timer;

    public void Configure(Node2D grabbedBy)
    {
        carrier = grabbedBy;
    }

    public override void Enter()
    {
        timer = MaxDuration;
        Entity.Velocity = Vector2.Zero;
        Visuals?.ShowPose(PoseAnimation, 0);
    }

    public override void PhysicsUpdate(double delta)
    {
        if (MaxDuration > 0f)
        {
            timer -= (float)delta;
            if (timer <= 0f)
            {
                Machine.PopState();
                return;
            }
        }

        // If the carrier is gone, stay frozen where it let go.
        if (IsInstanceValid(carrier) && !carrier.IsQueuedForDeletion())
        {
            Entity.GlobalPosition = carrier.GlobalPosition;
        }
    }
}
