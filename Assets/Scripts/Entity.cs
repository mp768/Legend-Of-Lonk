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
    [Export] public WeaponHolder Weapons { get; private set; }
    [Export] private Area2D hitbox;

    // Decides who this entity's attacks can hurt. Projectiles it fires inherit it.
    [Export] public Team Team { get; set; }

    // The direction the entity last moved in. Movement states update it, and attacks and knockback read it.
    public Vector2 FacingDirection { get; set; } = Vector2.Down;

    // The direction the entity is trying to move this tick, or zero. Movement states update it, and world
    // objects like pushable blocks read it.
    public Vector2 MoveIntent { get; set; }

    // Which clips this entity's damage plays.
    protected SoundPlayer.HurtSound hurtSound = SoundPlayer.HurtSound.ENEMY;
    protected SoundPlayer.DeathSound deathSound = SoundPlayer.DeathSound.ENEMY;

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

        StateMachine?.Start(this);
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

    // Pays for one shot of `weapon`. Returns false if the entity can't afford it. Enemies have infinite ammo.
    public virtual bool TrySpendAmmo(Weapon weapon) => true;

    public virtual void CycleWeapon()
    {
        Weapons?.CycleNext(_ => true);
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

            case Affectables.EffectType.STUN:
                // Value is the stun length in tenths of a second. A stun freezes without knockback or flashing.
                new StunCommand(Vector2.Zero, effect.Value / 10f, 0f, showHurt: false).Execute(this, StateMachine);
                break;

            case Affectables.EffectType.MAX_HEALTH:
                Health?.IncreaseMax(effect.Value);
                break;

            case Affectables.EffectType.GRAB:
                if (Health is not { IsInvincible: true } && new GrabCommand(effect).Execute(this, StateMachine))
                {
                    OnGrabbed();
                }
                break;
        }
    }

    protected abstract void Die();

    // Called after a grab has taken hold of the entity.
    protected virtual void OnGrabbed() { }

    protected void TakeDamage(int amount, Vector2 knockbackDirection)
    {
        if (Health == null || !Health.TakeDamage(amount))
        {
            return;
        }

        // Die has already run by now, via Health.Depleted.
        if (Health.IsDepleted)
        {
            SoundPlayer.Instance.PlayDeath(deathSound);
            return;
        }

        SoundPlayer.Instance.PlayHurt(hurtSound);
        Visuals?.Flash(Health.InvincibilityDuration);
        new StunCommand(knockbackDirection).Execute(this, StateMachine);
    }

    private void OnHitboxAreaEntered(Area2D area)
    {
        if (area is Affectables effect && effect.TryConsume())
        {
            SoundPlayer.Instance.PlayAttack(effect.HitSound);
            ApplyEffect(effect);
        }
    }
}
