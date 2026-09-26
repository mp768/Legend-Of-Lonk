using Godot;
using System;

public partial class UIDisplay : ColorRect
{
		private Label health;
		private Label rupees;
		private Label keys;
		private Label weapon;

		Label[] UIvalues = new Label[4];
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		health = GetNode<Label>("%Health");
		rupees = GetNode<Label>("%Rupees");
		keys = GetNode<Label>("%Keys");
		weapon = GetNode<Label>("%Weapon");

		// enum kind of system where we can store the actual variables and switch between which
		// ones we need to update for UI updates
		UIvalues[0] = health; // health = 1
		UIvalues[1] = rupees; // rupees = 2
		UIvalues[2] = keys; // keys = 3
		UIvalues[3] = weapon; // weapon = 4

		GameSignals.Instance.UpdateUI += updateUI;

		updateUI(0, 6);
		updateUI(0, 1);
		updateUI(0, 2);
	}

	private void updateUI(int value, int affected_label)
	{
		UIvalues[affected_label].Text = UIvalues[affected_label].Name + ": " + value;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}
}
