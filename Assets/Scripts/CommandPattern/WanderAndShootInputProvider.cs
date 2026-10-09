using Godot;

// Wanders like RandomWalkInputProvider and now and then fires its alternate weapon, e.g. a Goriya
// throwing its boomerang the way it's facing.
[GlobalClass]
public partial class WanderAndShootInputProvider : RandomWalkInputProvider
{
    [Export] public float MinFireInterval { get; set; } = 2f;
    [Export] public float MaxFireInterval { get; set; } = 4f;

    private static readonly AttackCommand FIRE = new(AttackState.AttackKind.ITEM);

    private Timer fireTimer;
    private bool wantsToFire;

    public override void _Ready()
    {
        base._Ready();

        fireTimer = new Timer { OneShot = true };
        fireTimer.Timeout += () => wantsToFire = true;
        AddChild(fireTimer);
        RestartFireTimer();
    }

    public override ICommand FetchNextCommand()
    {
        if (wantsToFire)
        {
            wantsToFire = false;
            RestartFireTimer();
            return FIRE;
        }

        return base.FetchNextCommand();
    }

    private void RestartFireTimer()
    {
        fireTimer.Start(GD.RandRange(MinFireInterval, MaxFireInterval));
    }
}
