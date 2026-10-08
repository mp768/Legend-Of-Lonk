using Godot;

// A pickup such as a rupee, key or heart. The collector's hitbox applies the effect, and the pickup removes itself.
[GlobalClass]
public partial class Collectible : Affectables
{
    public override void _Ready()
    {
        // Only collector areas count. A body brushing past shouldn't eat the pickup without applying it.
        AreaEntered += _ => QueueFree();
    }
}
