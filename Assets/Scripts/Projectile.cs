using Godot;

// An Affectables that moves: sword beams, arrows, boomerangs, fireballs and bombs. Whoever launches it
// decides its team, so it hurts the shooter's enemies no matter who is controlling the shooter.
// Scenes are authored facing right; launching rotates them to face their direction.
[GlobalClass]
public partial class Projectile : Affectables
{
    [Export] public float Speed { get; set; } = 180f;

    // Seconds before the projectile frees itself. 0 means it lives until something else ends it.
    [Export] public float Lifetime { get; set; }

    [Export] public bool PassesThroughWalls { get; set; }
    [Export] public bool DestroyOnHit { get; set; } = true;
    [Export] public bool RotateToDirection { get; set; } = true;
    [Export] public bool FreeWhenOffScreen { get; set; } = true;

    public Entity Shooter { get; private set; }

    private float age;

    // Places a new projectile in front of `shooter` and launches it. It's added under the shooter's
    // parent, so enemy projectiles live in (and pause with) their room.
    public static Projectile Spawn(PackedScene scene, Entity shooter, Vector2 direction, float distance)
    {
        if (scene?.Instantiate() is not Projectile projectile)
        {
            GD.PushError($"{scene?.ResourcePath} must have a {nameof(Projectile)} root.");
            return null;
        }

        Node parent = shooter.GetParent();
        Vector2 spawnPosition = shooter.GlobalPosition + direction.Normalized() * distance;
        projectile.Position = parent is Node2D parent2D ? parent2D.ToLocal(spawnPosition) : spawnPosition;
        projectile.Launch(shooter, direction);

        // Deferred because attacks usually start inside physics callbacks.
        parent.CallDeferred(Node.MethodName.AddChild, projectile);
        return projectile;
    }

    public virtual void Launch(Entity shooter, Vector2 direction)
    {
        Shooter = shooter;
        Direction = direction.Normalized();
        Team = shooter.Team;
        CollisionMask = TargetHurtboxLayerOf(Team) | (PassesThroughWalls ? 0 : Constants.WALLS_LAYER);

        if (RotateToDirection)
        {
            Rotation = direction.Angle();
        }
    }

    public override void _Ready()
    {
        AreaEntered += OnAreaEntered;
        BodyEntered += _ => OnHitWall();
        GameSignals.Instance.RoomTransitionStarted += OnRoomTransitionStarted;

        if (FreeWhenOffScreen)
        {
            var notifier = new VisibleOnScreenNotifier2D();
            notifier.ScreenExited += Destroy;
            AddChild(notifier);
        }
    }

    public override void _ExitTree()
    {
        GameSignals.Instance.RoomTransitionStarted -= OnRoomTransitionStarted;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        age += dt;
        if (Lifetime > 0f && age >= Lifetime)
        {
            OnLifetimeEnded();
            return;
        }

        GlobalPosition += Velocity() * dt;
    }

    // Stops the projectile and frees it at the end of the frame. Safe to call more than once.
    public void Destroy()
    {
        if (IsQueuedForDeletion())
        {
            return;
        }

        SetPhysicsProcess(false);
        SetDeferred(Area2D.PropertyName.Monitoring, false);
        SetDeferred(Area2D.PropertyName.Monitorable, false);
        QueueFree();
    }

    protected virtual Vector2 Velocity() => (Direction ?? Vector2.Zero) * Speed;

    protected virtual void OnAreaEntered(Area2D area) => OnHitTarget(area);

    protected virtual void OnHitTarget(Area2D target)
    {
        if (DestroyOnHit)
        {
            Destroy();
        }
    }

    protected virtual void OnHitWall() => Destroy();

    protected virtual void OnLifetimeEnded() => Destroy();

    // Projectiles never follow the player into another room.
    private void OnRoomTransitionStarted(Vector2 direction) => Destroy();
}
