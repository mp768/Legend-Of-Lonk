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

    // The direction the entity is trying to move this tick, or zero. States that move the entity update it.
    // Things like world objects like pushable blocks read it, and so does the knockback of the entity's contact areas.
    public Vector2 MoveIntent { get; set; }

    // Which clips this entity's damage plays.
    protected SoundPlayer.HurtSound hurtSound = SoundPlayer.HurtSound.ENEMY;
    protected SoundPlayer.DeathSound deathSound = SoundPlayer.DeathSound.ENEMY;

    // Effects this entity never accepts. Areas carrying them are left untouched rather than consumed.
    protected readonly HashSet<Affectables.EffectType> ignoredEffects = [];

    private readonly List<Area2D> contactAreas = [];

    public override void _Ready()
    {
        foreach (Node child in GetChildren())
        {
            // Contact areas on the entity (like an enemy's contact damage).
            if (child is Affectables contact)
            {
                contact.Source = this;
                contactAreas.Add(contact);
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

    // Pays for one shot of `weapon`. Returns false if the entity can't afford it. Enemies have infinite ammo.
    public virtual bool TrySpendAmmo(Weapon weapon) => true;

    public virtual void CycleWeapon()
    {
        Weapons?.CycleNext(_ => true);
    }

    // Turns the entity's hitbox and contact areas on or off, e.g. while it's dormant. Disabling the shapes, not
    // Monitoring, also hides it from ray checks like a blade trap's line of sight.
    public void SetTangible(bool tangible)
    {
        if (hitbox != null)
        {
            SetShapesDisabled(hitbox, !tangible);
        }

        foreach (Area2D area in contactAreas)
        {
            SetShapesDisabled(area, !tangible);
        }
    }

    protected virtual void ApplyEffect(Affectables effect)
    {
        switch (effect.Type)
        {
            case Affectables.EffectType.DAMAGE:
                TakeDamage(effect.Value, KnockbackFrom(effect));
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

    protected Vector2 KnockbackFrom(Affectables effect) => effect.KnockbackDirectionFor(this) ?? -FacingDirection;

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
        if (area is Affectables effect && !ignoredEffects.Contains(effect.Type) && effect.TryConsume())
        {
            SoundPlayer.Instance.PlayAttack(effect.HitSound);
            ApplyEffect(effect);
        }
    }

    private static void SetShapesDisabled(Area2D area, bool disabled)
    {
        foreach (Node child in area.GetChildren())
        {
            if (child is CollisionShape2D shape)
            {
                // Deferred because entities go dormant from inside hitbox callbacks.
                shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, disabled);
            }
        }
    }
}
