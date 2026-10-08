using Godot;

// The player character. Everything he shares with other entities lives in Entity and its components.
// This script only reports his game-wide consequences out to GameSignals and GameState.
public partial class Lonk : Entity
{
	[Export] private TransitionState transitionState;

	public override void _Ready()
	{
		base._Ready();
		AddToGroup(Constants.PLAYER_GROUP);

		Health.Changed += OnHealthChanged;
		GameSignals.Instance.CheatModeChanged += OnCheatModeChanged;
		GameSignals.Instance.RoomTransitionStarted += OnRoomTransitionStarted;
		GameSignals.Instance.RoomTransitionFinished += OnRoomTransitionFinished;

		OnHealthChanged(Health.Current, Health.Max);
	}

	public override void _ExitTree()
	{
		GameSignals.Instance.CheatModeChanged -= OnCheatModeChanged;
		GameSignals.Instance.RoomTransitionStarted -= OnRoomTransitionStarted;
		GameSignals.Instance.RoomTransitionFinished -= OnRoomTransitionFinished;
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

			default:
				base.ApplyEffect(effect);
				break;
		}
	}

	protected override void Die()
	{
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerDied);
	}

	private void OnHealthChanged(int current, int max)
	{
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerHealthChanged, current);
	}

	private void OnCheatModeChanged(bool enabled)
	{
		Health.IsImmune = enabled;
		if (enabled)
		{
			Health.Refill();
		}
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
	}
}
