using Godot;

// Knocks the entity back while it holds a hurt pose. It can't be interrupted, so repeated hits don't stack stuns.
// Each push can override the duration and knockback, e.g. a boomerang stun that freezes without knockback.
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
    private float duration;
    private float knockbackSpeed;
    private bool showHurt;

    // Null duration or speed uses the exported defaults.
    public void Configure(Vector2 direction, float? stunDuration = null, float? speed = null, bool hurt = true)
    {
        knockbackDirection = direction.Normalized();
        duration = stunDuration ?? Duration;
        knockbackSpeed = speed ?? KnockbackSpeed;
        showHurt = hurt;
    }

    public override void Enter()
    {
        timer = duration;
        delayTimer = KnockbackDelay;

        if (showHurt)
        {
            Visuals?.ShowPose(PoseAnimation, PoseFrame);
        }
        if (showHurt && FlashWhileStunned)
        {
            Visuals?.Flash(duration);
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
            Entity.Velocity = knockbackDirection * knockbackSpeed;
        }

        Entity.MoveAndSlide();
    }

    public override void Exit()
    {
        Entity.Velocity = Vector2.Zero;
        if (showHurt && FlashWhileStunned)
        {
            Visuals?.StopFlash();
        }
    }
}
