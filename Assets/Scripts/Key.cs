using Godot;
using System;

public partial class Key : Affectables
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		base._Ready();
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node area)
	{
		GD.Print("entered");
		QueueFree();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
