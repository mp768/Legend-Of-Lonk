using Godot;

// A locked door that opens when the player touches it while holding enough keys.
[GlobalClass]
public partial class LockedDoor : Door
{
	[Export] private Area2D trigger;
	[Export] public int KeyCost { get; set; } = 1;
	[Export] private Door otherSideDoor;

	private bool opening;

	public override void _Ready()
	{
		base._Ready();
		trigger.BodyEntered += OnTriggerBodyEntered;
	}

	private async void OnTriggerBodyEntered(Node2D body)
	{
		if (opening || IsOpen || !body.IsInGroup(Constants.PLAYER_GROUP) || !GameState.Instance.TryUseKeys(KeyCost))
		{
			return;
		}

		opening = true;

		// A short pause so the door doesn't vanish the instant it's touched.
		await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForDeletion())
		{
			return;
		}

		Open();

		if (otherSideDoor != null) otherSideDoor.Open();
	}
}
