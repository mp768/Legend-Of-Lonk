using Godot;

[GlobalClass]
public partial class RoomWarp : Area2D
{
	[Export] public Room Destination { get; set; }

	// Where we should appear in the destination room.
	[Export] public Marker2D Arrival { get; set; }

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (Destination == null || Arrival == null || !body.IsInGroup(Constants.PLAYER_GROUP))
		{
			return;
		}

		GameSignals.Instance.EmitSignal(GameSignals.SignalName.WarpRequested, Destination, body, Arrival.GlobalPosition);
	}
}
