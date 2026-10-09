using Godot;

// Flies out a fixed distance, or until it hits a wall or a target, then homes back to whoever threw it.
// Pickups it touches ride back with it, so the thrower collects them the normal way on its return.
[GlobalClass]
public partial class BoomerangProjectile : Projectile
{
    [Export] public float Range { get; set; } = 96f;

    // How close to the thrower the boomerang has to get to be caught.
    [Export] public float CatchDistance { get; set; } = 6f;

    // Whether pickups touched on the way are carried back.
    [Export] public bool FetchesPickups { get; set; } = true;

    private Vector2 origin;
    private bool returning;
    private bool caught;

    public override void Launch(Entity shooter, Vector2 direction)
    {
        base.Launch(shooter, direction);

        if (FetchesPickups)
        {
            CollisionMask |= Constants.COLLECTIBLE_LAYER;
        }
    }

    public override void _Ready()
    {
        base._Ready();
        origin = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(Shooter) || Shooter.IsQueuedForDeletion())
        {
            Destroy();
            return;
        }

        if (!returning && GlobalPosition.DistanceTo(origin) >= Range)
        {
            Return();
        }

        if (returning)
        {
            Vector2 toShooter = Shooter.GlobalPosition - GlobalPosition;
            if (toShooter.Length() <= CatchDistance)
            {
                Catch();
                return;
            }

            Direction = toShooter.Normalized();
        }

        base._PhysicsProcess(delta);
    }

    protected override void OnAreaEntered(Area2D area)
    {
        if (area is Collectible pickup)
        {
            // A carried pickup re-enters the area each time it's reparented, so only grab it once, and
            // never take back what's being handed over on the catch.
            // Reparenting changes the tree, which can't happen mid physics callback.
            if (!caught && !pickup.IsCollected && pickup.GetParent() != this)
            {
                pickup.CallDeferred(Node.MethodName.Reparent, this);
            }
            return;
        }

        base.OnAreaEntered(area);
    }

    protected override void OnHitTarget(Area2D target) => Return();

    protected override void OnHitWall() => Return();

    private void Return()
    {
        if (returning)
        {
            return;
        }

        returning = true;

        // The way back homes straight to the thrower, over walls if it has to.
        SetDeferred(Area2D.PropertyName.CollisionMask, CollisionMask & ~Constants.WALLS_LAYER);
    }

    private void Catch()
    {
        caught = true;

        // Drop anything carried at the thrower's feet, where their collector picks it up, before the
        // boomerang (and its children) are freed.
        Node parent = GetParent();
        foreach (Node child in GetChildren())
        {
            if (child is Collectible pickup)
            {
                pickup.CallDeferred(Node.MethodName.Reparent, parent);
            }
        }

        Destroy();
    }
}
