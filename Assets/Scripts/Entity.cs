using Godot;
using System.Collections.Generic;

// Shared root for anything driven by a PushdownStateMachine: the player character, enemies, and so on.
// It wires the hitbox, health, presenter and state machine together and applies the effects every entity
// reacts to the same way. Subclasses override ApplyEffect and Die for reactions of their own.
public abstract partial class Entity : CharacterBody2D
{
    [Export] public PushdownStateMachine StateMachine { get; private set; }
    [Export] public SpritePresenter Visuals { get; private set; }
    [Export] public Health Health { get; private set; }
    [Export] private Area2D hitbox;

    // The direction the entity last moved in. Movement states update it, and attacks and knockback read it.
    public Vector2 FacingDirection { get; set; } = Vector2.Down;

    private readonly List<IInputProvider> inputProviders = [];

    public override void _Ready()
    {
        foreach (Node child in GetChildren())
        {
            if (child is IInputProvider provider)
            {
                inputProviders.Add(provider);
            }
        }

        if (hitbox != null)
        {
            hitbox.AreaEntered += OnHitboxAreaEntered;
        }

        if (Health != null)
        {
            Health.Depleted += Die;
        }

        StateMachine.Start(this);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Debug toggle that demonstrates swappable controllers.
        if (@event.IsActionPressed("swap"))
        {
            CycleInputProvider();
        }
    }

    // Hands control to the next IInputProvider among the entity's children.
    public void CycleInputProvider()
    {
        if (inputProviders.Count == 0)
        {
            return;
        }

        int next = (inputProviders.IndexOf(StateMachine.InputProvider) + 1) % inputProviders.Count;
        StateMachine.InputProvider = inputProviders[next];
    }

    protected virtual void ApplyEffect(Affectables effect)
    {
        switch (effect.Type)
        {
            case Affectables.EffectType.DAMAGE:
                // Sources without a direction knock the entity back the way it came.
                TakeDamage(effect.Value, effect.Direction ?? -FacingDirection);
                break;

            case Affectables.EffectType.HEALTH:
                Health?.Heal(effect.Value);
                break;
        }
    }

    protected abstract void Die();

    private void TakeDamage(int amount, Vector2 knockbackDirection)
    {
        if (Health == null || !Health.TakeDamage(amount) || Health.IsDepleted)
        {
            return;
        }

        Visuals?.Flash(Health.InvincibilityDuration);
        new StunCommand(knockbackDirection).Execute(this, StateMachine);
    }

    private void OnHitboxAreaEntered(Area2D area)
    {
        if (area is Affectables effect)
        {
            ApplyEffect(effect);
        }
    }
}
