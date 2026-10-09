using Godot;

// Root script for enemies. Behaviour comes from the scene's states and input providers, so a new enemy
// is usually a new scene rather than a new script.
[GlobalClass]
public partial class Enemy : Entity, IResettableEntity
{
    // Emitted when the enemy is killed. Unlike TreeExiting, it doesn't fire when a scene reload frees it.
    [Signal]
    public delegate void DefeatedEventHandler();

    [Export] public DropTable Drops { get; set; }

    // Whether its room waits for it to be defeated before counting as cleared. Off for invincible
    // hazards like blade traps.
    [Export] public bool CountsTowardClear { get; set; } = true;

    // Whether a stun (e.g. the boomerang) is enough to kill it outright.
    [Export] public bool StunKills { get; set; }

    private Vector2 spawnPosition;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup(Constants.ENEMY_GROUP);
        spawnPosition = GlobalPosition;
    }

    public void OnRoomEntered()
    {
        GlobalPosition = spawnPosition;
    }

    protected override void ApplyEffect(Affectables effect)
    {
        if (effect.Type == Affectables.EffectType.STUN && StunKills && Health != null)
        {
            TakeDamage(Health.Current, effect.Direction ?? -FacingDirection);
            return;
        }

        base.ApplyEffect(effect);
    }

    protected override void Die()
    {
        // Death is triggered from a hitbox callback. Stop simulating now and let the free happen at the end of the frame.
        StateMachine.SetPhysicsProcess(false);
        SpawnDrop();
        QueueFree();

        // Emitted after QueueFree so listeners counting what's left can already see this enemy as gone.
        EmitSignal(SignalName.Defeated);
    }

    private void SpawnDrop()
    {
        if (Drops?.Roll()?.Instantiate() is not Node2D item)
        {
            return;
        }

        // The drop joins the enemy's room, so it stays behind when the room is left.
        item.Position = Position;
        GetParent().CallDeferred(Node.MethodName.AddChild, item);
    }
}
