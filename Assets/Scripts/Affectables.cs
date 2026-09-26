using Godot;
using System;

[GlobalClass]
public partial class Affectables : Area2D
{
	[Export] private GridMovementComponent _movementComponent;
	[Export] private GridAnimationComponent _animationComponent;
	private int effect_value = 0 ;

	public enum Effects
	{
		DAMAGE,
		HEALTH,
		RUPEES,
		KEYS,
	}

	private Effects single_effect;

	public int GetEffect()
	{
		return effect_value;
	}

	public void AssignEffect(Effects e)
	{
		single_effect = e;
	}

	public Effects GetEffectType()
	{
		return single_effect;
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
