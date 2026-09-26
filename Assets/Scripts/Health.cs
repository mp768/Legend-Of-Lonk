using Godot;
using System;

public partial class Health : Node
{
	public int health; // Save health as an int, half a heart = 1 health, so max HP is 6
	public int maxHealth;
	public int damageMultiplier = 1;

	public Health(int maxHP)
	{
		this.health = maxHP;
		this.maxHealth = maxHP;
	}

	// for next time, make this function return true or false: false if the heath reaches 0
	public void applyHealthEffect(int dmg)
	{
		// damageMultiplier can be set to 0 to have no effect on player
		health = Mathf.Clamp((health + dmg)* damageMultiplier, 0, maxHealth);
	}

	public void HealthCheat(bool set)
	{
		if (set)
		{
			health = maxHealth;
			damageMultiplier = 0;	
		}
		else
		{
			damageMultiplier = 1;
		}
		
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
