using Godot;

// Stairs or a ladder that cuts straight to another room when the player character steps onto it.
// A hidden warp does nothing until revealed, e.g. by a pushable block's Pushed signal.
[GlobalClass]
public partial class RoomWarp : Area2D
{
	[Export] public Room Destination { get; set; }

	// Where the traveler appears in the destination room. Keep it off the destination's own warps.
	[Export] public Marker2D Arrival { get; set; }

	[Export] public bool StartsHidden { get; set; }

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;

		if (StartsHidden)
		{
			Visible = false;
			SetDeferred(Area2D.PropertyName.Monitoring, false);
		}
	}

	public void Reveal()
	{
		Visible = true;
		SetDeferred(Area2D.PropertyName.Monitoring, true);
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
