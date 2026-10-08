using Godot;

// Knocks the entity back while it holds a hurt pose. It can't be interrupted, so repeated hits don't stack stuns.
[GlobalClass]
public partial class StunState : State
{
    [ExportGroup("Knockback")]
    [Export] public float Duration { get; set; } = 0.5f;
    [Export] public float KnockbackSpeed { get; set; } = 120f;

    // How long the entity freezes in place at the start of the stun before it gets pushed.
    [Export] public float KnockbackDelay { get; set; }

    [ExportGroup("Pose")]
    [Export] public string PoseAnimation { get; set; } = "hurt";
    [Export] public int PoseFrame { get; set; }
    [Export] public bool FlashWhileStunned { get; set; } = true;

    public override bool IsInterruptable => false;

    private float timer;
    private float delayTimer;
    private Vector2 knockbackDirection;

    public void Configure(Vector2 direction)
    {
        knockbackDirection = direction.Normalized();
    }

    public override void Enter()
    {
        timer = Duration;
        delayTimer = KnockbackDelay;

        Visuals?.ShowPose(PoseAnimation, PoseFrame);
        if (FlashWhileStunned)
        {
            Visuals?.Flash(Duration);
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        float dt = (float)delta;

        timer -= dt;
        if (timer <= 0f)
        {
            Machine.PopState();
            return;
        }

        if (delayTimer > 0f)
        {
            delayTimer -= dt;
            Entity.Velocity = Vector2.Zero;
        }
        else
        {
            Entity.Velocity = knockbackDirection * KnockbackSpeed;
        }

        Entity.MoveAndSlide();
    }

    public override void Exit()
    {
        Entity.Velocity = Vector2.Zero;
        if (FlashWhileStunned)
        {
            Visuals?.StopFlash();
        }
    }
}
