using Godot;
using System;

// What a player may do in the world. Stored as a number in scenes, so only append members.
[Flags]
public enum PlayerAbility
{
    NONE = 0,
    OPEN_DOORS = 1 << 0,
    PUSH_BLOCKS = 1 << 1,
    TAKE_STAIRS = 1 << 2,
}

// An entity the player can be: Lonk, or an enemy he has possessed. Exactly one is the player at a time, by
// being in the player group. Only that one moves between rooms and has its stats on the HUD. Whatever it
// collects goes into GameState.
public abstract partial class PlayerEntity : Entity
{
    [Export] public PlayerAbility Abilities { get; set; } = PlayerAbility.OPEN_DOORS | PlayerAbility.PUSH_BLOCKS | PlayerAbility.TAKE_STAIRS;

    [Export] private TransitionState transitionState;

    public bool IsPlayer => IsInGroup(Constants.PLAYER_GROUP);

    public override void _Ready()
    {
        base._Ready();
        hurtSound = SoundPlayer.HurtSound.PLAYER;

        // A vessel can be spawned while cheats are already on.
        Health.IsImmune = GameState.Instance.CheatsEnabled;

        Health.Changed += OnHealthChanged;
        GameSignals.Instance.CheatModeChanged += OnCheatModeChanged;
        GameSignals.Instance.RoomTransitionStarted += OnRoomTransitionStarted;
        GameSignals.Instance.RoomTransitionFinished += OnRoomTransitionFinished;
    }

    public override void _ExitTree()
    {
        GameSignals.Instance.CheatModeChanged -= OnCheatModeChanged;
        GameSignals.Instance.RoomTransitionStarted -= OnRoomTransitionStarted;
        GameSignals.Instance.RoomTransitionFinished -= OnRoomTransitionFinished;
    }

    // Whether `body` is the player right now and is able to do `ability`. For world objects like doors.
    public static bool CanPlayerDo(Node body, PlayerAbility ability) =>
        body is PlayerEntity player && player.IsPlayer && player.Abilities.HasFlag(ability);

    // Makes this body the player and puts its stats on the HUD.
    public void TakePlayerRole()
    {
        AddToGroup(Constants.PLAYER_GROUP);
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerHealthChanged, Health.Current, Health.Max);
        ReportWeapon();
    }

    public void DropPlayerRole()
    {
        RemoveFromGroup(Constants.PLAYER_GROUP);
    }

    public override bool TrySpendAmmo(Weapon weapon)
    {
        return weapon.Ammo switch
        {
            AmmoType.RUPEES => GameState.Instance.TrySpendRupees(weapon.AmmoCost),
            AmmoType.BOMBS => GameState.Instance.TrySpendBombs(weapon.AmmoCost),
            _ => true,
        };
    }

    protected override void ApplyEffect(Affectables effect)
    {
        switch (effect.Type)
        {
            case Affectables.EffectType.RUPEES:
                GameState.Instance.AddRupees(effect.Value);
                break;

            case Affectables.EffectType.KEYS:
                GameState.Instance.AddKeys(effect.Value);
                break;

            case Affectables.EffectType.BOMBS:
                GameState.Instance.AddBombs(effect.Value);
                GameState.Instance.UnlockWeapon(AltWeapon.BOMB);
                break;

            case Affectables.EffectType.WEAPON:
                GameState.Instance.UnlockWeapon((AltWeapon)effect.Value);
                break;

            default:
                base.ApplyEffect(effect);
                break;
        }
    }

    protected void ReportWeapon()
    {
        if (IsPlayer)
        {
            GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerWeaponChanged, Weapons?.Selected);
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        if (IsPlayer)
        {
            GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerHealthChanged, current, max);
        }
    }

    private void OnCheatModeChanged(bool enabled)
    {
        Health.IsImmune = enabled;
        if (enabled)
        {
            Health.Refill();
        }
    }

    // Only the player is carried into the next room. Anyone else, like a dormant Lonk, stays where they are.
    private void OnRoomTransitionStarted(Vector2 direction)
    {
        if (!IsPlayer || transitionState == null)
        {
            return;
        }

        transitionState.Configure(direction);
        StateMachine.PushState(transitionState);
    }

    private void OnRoomTransitionFinished()
    {
        if (StateMachine.CurrentState == transitionState)
        {
            StateMachine.PopState();
        }

        // A grab ends with the warp it causes.
        if (StateMachine.CurrentState is GrabbedState)
        {
            StateMachine.PopState();
        }
    }
}
