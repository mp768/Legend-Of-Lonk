using Godot;

// Root script for enemies. Behaviour comes from the scene's states and input providers, so a new enemy
// is usually a new scene rather than a new script.
[GlobalClass]
public partial class Enemy : Entity, IResettableEntity
{
    private Vector2 spawnPosition;

    public override void _Ready()
    {
        base._Ready();
        spawnPosition = GlobalPosition;
    }

    public void OnRoomEntered()
    {
        GlobalPosition = spawnPosition;
    }

    protected override void Die()
    {
        // Death is triggered from a hitbox callback. Stop simulating now and let the free happen at the end of the frame.
        StateMachine.SetPhysicsProcess(false);
        QueueFree();
    }
}
