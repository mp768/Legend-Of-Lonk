using Godot;
using System;

public partial class Key : Affectables
{
	public override void _Ready()
	{
		BodyEntered += (_) => OnEnter();
		AreaEntered += (_) => OnEnter();
	}

	private void OnEnter()
	{
		QueueFree();
	}
}
