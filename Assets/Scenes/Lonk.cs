using Godot;
using System;

public partial class Lonk : CharacterBody2D
{
	public const float Speed = 300.0f;
	public const float JumpVelocity = -400.0f;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;
		Vector2 direction = Vector2.Zero;
		//up + down has priority over left/right

		int up = Input.IsActionPressed("up") ? 1 : 0 ;
		int down = Input.IsActionPressed("down") ? 1: 0;
		int left = Input.IsActionPressed("left") ? 1: 0;
		int right = Input.IsActionPressed("right") ? 1: 0;

		// up || down	
		if (up + down >= 1) // int equivalent of booleans where at least one of them is true
		{
			direction += Vector2.Up * up + Vector2.Down * down;
		// left || right
		} else if (left + right >= 1) {
			direction += Vector2.Left * left + Vector2.Right * right;
		}

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.
		
		if (direction != Vector2.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Y = direction.Y * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
