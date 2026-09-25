using Godot;
using System;

public partial class LockedDoor : TileMapLayer
{
	[Export] private Area2D _lockedDoor;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_lockedDoor.AreaEntered += OnAreaEntered;
	}

	private async void OnAreaEntered(Area2D _)
	{
		// TODO: A check should be here to check if we have a key to open a door.

		// A little timer that we set so that the door doesn't look like it opens up immediately.
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		QueueFree();
	} 
}
