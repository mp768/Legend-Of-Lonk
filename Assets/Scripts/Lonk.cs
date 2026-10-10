using Godot;

public partial class Lonk : Entity
{
    [Export] private TransitionState transitionState;
    [Export] private SideScrollMovementState sideScrollState;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup(Constants.PLAYER_GROUP);

        hurtSound = SoundPlayer.HurtSound.PLAYER;
        deathSound = SoundPlayer.DeathSound.PLAYER;

        Health.Changed += OnHealthChanged;
        GameSignals.Instance.CheatModeChanged += OnCheatModeChanged;
        GameSignals.Instance.SelectedWeaponChanged += OnSelectedWeaponChanged;
        GameSignals.Instance.RoomTransitionStarted += OnRoomTransitionStarted;
        GameSignals.Instance.RoomTransitionFinished += OnRoomTransitionFinished;
        GameSignals.Instance.RoomViewChanged += OnRoomViewChanged;

        OnHealthChanged(Health.Current, Health.Max);
        Weapons?.Select(GameState.Instance.SelectedWeapon);
    }

    public override void _ExitTree()
    {
        GameSignals.Instance.CheatModeChanged -= OnCheatModeChanged;
        GameSignals.Instance.SelectedWeaponChanged -= OnSelectedWeaponChanged;
        GameSignals.Instance.RoomTransitionStarted -= OnRoomTransitionStarted;
        GameSignals.Instance.RoomTransitionFinished -= OnRoomTransitionFinished;
        GameSignals.Instance.RoomViewChanged -= OnRoomViewChanged;
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

    // His selection is global game data, so it goes through GameState, which reports back the new choice.
    public override void CycleWeapon()
    {
        GameState.Instance.CycleWeapon();
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

    protected override void Die()
    {
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerDied);
    }

    protected override void OnGrabbed()
    {
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerGrabbed, this);
    }

    private void OnHealthChanged(int current, int max)
    {
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerHealthChanged, current, max);
    }

    private void OnCheatModeChanged(bool enabled)
    {
        Health.IsImmune = enabled;
        if (enabled)
        {
            Health.Refill();
        }
    }

    private void OnSelectedWeaponChanged(int kind)
    {
        Weapons?.Select(kind < 0 ? null : (AltWeapon)kind);
    }

    private void OnRoomTransitionStarted(Vector2 direction)
    {
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

    // Which kind of movement he uses is decided by the room he's in.
    private void OnRoomViewChanged(int view)
    {
        bool sideScrolling = (Room.RoomView)view == Room.RoomView.SIDE_SCROLL;
        bool inSideScrollState = StateMachine.CurrentState == sideScrollState;

        if (sideScrolling && !inSideScrollState && sideScrollState != null)
        {
            StateMachine.PushState(sideScrollState);
        }
        else if (!sideScrolling && inSideScrollState)
        {
            StateMachine.PopState();
        }
    }
}
