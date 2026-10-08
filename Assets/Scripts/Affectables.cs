using Godot;

// Describes an effect on whatever touches this area. The receiving entity decides how to apply it.
// Inherits Area2D because every affectable is a contact area.
[GlobalClass]
public partial class Affectables : Area2D
{
    // Values are stored by index in scenes, so only append new members.
    public enum EffectType
    {
        DAMAGE,
        HEALTH,
        RUPEES,
        KEYS,
    }

    [Export] public EffectType Type { get; set; }
    [Export] public int Value { get; set; }

    // Optional knockback direction for the receiver, e.g. the way a sword is swinging.
    public Vector2? Direction { get; set; }
}
