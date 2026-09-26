using Godot;
using System;

public partial class Rupee : Affectables
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
