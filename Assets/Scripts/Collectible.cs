using Godot;

// A pickup such as a rupee, key or heart. The collector's hitbox applies the effect, and the pickup removes
// itself. Each pickup can only be collected once.
[GlobalClass]
public partial class Collectible : Affectables
{
    [Export] public SoundPlayer.CollectSound CollectSound { get; set; }

    public bool IsCollected { get; private set; }

    // A moving pickup (e.g. carried by a boomerang) can be reported to a collector several times before it's
    // freed, so only the first report counts.
    public override bool TryConsume()
    {
        if (IsCollected)
        {
            return false;
        }

        IsCollected = true;
        SoundPlayer.Instance.PlayCollect(CollectSound);
        SetDeferred(Area2D.PropertyName.Monitorable, false);
        QueueFree();
        return true;
    }
}
