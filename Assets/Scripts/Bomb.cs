using Godot;

// A dropped bomb. It sits still and harmless until its fuse runs out, then its area becomes a blast that
// damages the bomber's enemies for a moment before it disappears.
[GlobalClass]
public partial class Bomb : Projectile
{
    [Export] private Timer fuse;
    [Export] private CanvasItem bombVisual;
    [Export] private CanvasItem blastVisual;

    [Export] public float BlastDuration { get; set; } = 0.15f;

    public override void _Ready()
    {
        base._Ready();

        Monitorable = false;
        if (blastVisual != null)
        {
            blastVisual.Visible = false;
        }

        fuse.Timeout += Explode;
        fuse.Start();
    }

    public override void Launch(Entity shooter, Vector2 direction)
    {
        base.Launch(shooter, direction);

        // The blast pushes victims away from wherever they are, not in the throw direction.
        Direction = null;
    }

    protected override Vector2 Velocity() => Vector2.Zero;

    // A bomb on the floor isn't stopped by anything it touches.
    protected override void OnHitTarget(Area2D target) { }

    protected override void OnHitWall() { }

    private void Explode()
    {
        SetDeferred(Area2D.PropertyName.Monitorable, true);

        if (bombVisual != null)
        {
            bombVisual.Visible = false;
        }
        if (blastVisual != null)
        {
            blastVisual.Visible = true;
        }

        // The fuse is a child Timer, so it can't fire after the bomb is gone.
        fuse.Timeout -= Explode;
        fuse.Timeout += Destroy;
        fuse.Start(BlastDuration);
    }
}
