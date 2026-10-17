using Godot;

public partial class Lonk : PlayerEntity
{
	[Export] private SideScrollMovementState sideScrollState;

	public override void _Ready()
	{
		base._Ready();
		deathSound = SoundPlayer.DeathSound.PLAYER;

		GameSignals.Instance.SelectedWeaponChanged += OnSelectedWeaponChanged;
		GameSignals.Instance.RoomViewChanged += OnRoomViewChanged;

		Weapons?.Select(GameState.Instance.SelectedWeapon);
		TakePlayerRole();
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		GameSignals.Instance.SelectedWeaponChanged -= OnSelectedWeaponChanged;
		GameSignals.Instance.RoomViewChanged -= OnRoomViewChanged;
	}

	// His selection is global game data, so it goes through GameState, which reports back the new choice.
	public override void CycleWeapon()
	{
		GameState.Instance.CycleWeapon();
	}

	protected override void Die()
	{
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerDied);
	}

	protected override void OnGrabbed()
	{
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.PlayerGrabbed, this);
	}

	private void OnSelectedWeaponChanged(int kind)
	{
		Weapons?.Select(kind < 0 ? null : (AltWeapon)kind);
		ReportWeapon();
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
