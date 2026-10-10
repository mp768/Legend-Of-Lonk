using Godot;

// Which side an entity or attack is on. An attack only hurts the opposing team.
public enum Team
{
    PLAYER,
    ENEMY,
}

// Describes an affect on whatever touches this area. The receiving entity decides how to apply it.
// Inherits Area2D because every affectable is something that can be contacted with.
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
        STUN,
        BOMBS,
        WEAPON,
        MAX_HEALTH,
        GRAB,
    }

    [Export] public EffectType Type { get; set; }
    [Export] public int Value { get; set; }

    // The clip a receiver plays when it accepts this effect, e.g. a sword connecting.
    [Export] public SoundPlayer.AttackSound HitSound { get; set; }

    // Optional knockback direction for the receiver, e.g. the way a sword is swinging.
    public Vector2? Direction { get; set; }

    // The entity this area is attached to, e.g. the enemy behind a contact damage area. Null for areas that
    // stand on their own, like projectiles and pickups.
    public Entity Source { get; set; }

    private Team team;

    // Moves the area onto its team's attack layer. Set on spawned attacks, whose team comes from whoever
    // fired them. Areas placed in a scene have their layer set in the editor instead.
    public Team Team
    {
        get => team;
        set
        {
            team = value;
            CollisionLayer = AttackLayerOf(value);
        }
    }

    // Called by a receiver before it applies this effect. Returns false if the effect has already been
    // used up and must not be applied again. Reusable effects, like a sword, can always be applied.
    public virtual bool TryConsume() => true;

    public Vector2? KnockbackDirectionFor(Node2D receiver)
    {
        if (Direction != null || Source == null || Source.MoveIntent == Vector2.Zero)
        {
            return Direction;
        }

        Vector2 heading = Source.MoveIntent.Normalized();
        bool inPath = heading.Dot(receiver.GlobalPosition - Source.GlobalPosition) > 0f;
        return inPath ? heading : null;
    }

    public static uint AttackLayerOf(Team team) =>
        team == Team.PLAYER ? Constants.PLAYER_ATTACK_LAYER : Constants.ENEMY_ATTACK_LAYER;

    // The hurtbox layer an attack from `team` can hit.
    public static uint TargetHurtboxLayerOf(Team team) =>
        team == Team.PLAYER ? Constants.ENEMY_HURTBOX_LAYER : Constants.PLAYER_HURTBOX_LAYER;
}
