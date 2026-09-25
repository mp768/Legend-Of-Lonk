using Godot;
using System;

public partial class Lonk : CharacterBody2D
{
    [Export] public float MoveSpeed = 60.0f;
	[Export] private Vector2I spriteOffset;

    [Export] private AnimatedSprite2D _sprite;

    private int _movementFrameCounter = 0;

	// This is the frequency I observed when looking at footage of someone playing the game.
    private const int ANIMATION_STEP_FREQUENCY = 6;

	// The grid size that link snaps to when changing axes.
    private const float GRID_SIZE = 8.0f;

	private enum Axis { None, Horizontal, Vertical }
	private Axis _primaryAxis = Axis.None;

    public override void _Ready()
    {
        if (_sprite != null)
        {
            _sprite.Stop(); // This allows us to take control of the animation manually.
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float fDelta = (float)delta;
        Vector2 inputDir = GetCardinalInput(fDelta);

        if (inputDir != Vector2.Zero)
        {
            // Apply strict grid alignment velocity on the perpendicular axis
            Velocity = CalculateGridAlignedVelocity(inputDir, fDelta, snapPosition: true);

            MoveAndSlide();

            UpdateFacingAndAnimation(inputDir);
            AdvanceAnimationFrame();
        }
        else
        {
            Velocity = Vector2.Zero;
            _movementFrameCounter = 0;
            MoveAndSlide();
        }

        // Keep visuals fixed while not messing with the physics position.
        if (_sprite != null)
        {
            _sprite.Position = new Vector2(
                Mathf.Round(Position.X) - Position.X + spriteOffset.X,
                Mathf.Round(Position.Y) - Position.Y + spriteOffset.Y
            );
        }
    }

	private Vector2 GetCardinalInput(float delta)
	{
		float x = Input.GetAxis("left", "right");
		float y = Input.GetAxis("up", "down");

		bool hasX = !Mathf.IsZeroApprox(x);
		bool hasY = !Mathf.IsZeroApprox(y);

		if (!hasX && !hasY)
		{
			_primaryAxis = Axis.None;
			return Vector2.Zero;
		}

		Vector2 vertDir = hasY ? new Vector2(0.0f, Mathf.Sign(y)) : Vector2.Zero;
		Vector2 horzDir = hasX ? new Vector2(Mathf.Sign(x), 0.0f) : Vector2.Zero;

		// If both directions are held, prioritize the initial axis, fallback to perpendicular if blocked
		if (hasY && hasX)
		{
			if (_primaryAxis == Axis.Horizontal)
			{
				Vector2 horzVel = CalculateGridAlignedVelocity(horzDir, delta, snapPosition: false);
				if (!TestMove(GlobalTransform, horzVel * delta))
				{
					return horzDir; // Horizontal path is clear
				}
				return vertDir; // Horizontal blocked by wall. fall back to vertical
			}
			else if (_primaryAxis == Axis.Vertical)
			{
				Vector2 vertVel = CalculateGridAlignedVelocity(vertDir, delta, snapPosition: false);
				if (!TestMove(GlobalTransform, vertVel * delta))
				{
					return vertDir; // Vertical path is clear
				}
				return horzDir; // Vertical blocked by wall. fall back to horizontal
			}
		}

		if (hasY)
		{
			_primaryAxis = Axis.Vertical;
			return vertDir;
		}

		_primaryAxis = Axis.Horizontal;
		return horzDir;
	}

    /// <summary>
    /// Steers and snaps Link's perpendicular axis onto the nearest grid box.
    /// </summary>
    private Vector2 CalculateGridAlignedVelocity(Vector2 inputDir, float delta, bool snapPosition = true)
    {
        Vector2 velocity = inputDir * MoveSpeed;

        // Moving VERTICALLY: Pull X axis toward nearest grid box
        if (inputDir.Y != 0.0f)
        {
            float targetX = Mathf.Round(Position.X / GRID_SIZE) * GRID_SIZE;
            float diffX = targetX - Position.X;

            if (Mathf.Abs(diffX) > 0.01f)
            {
                if (Mathf.Abs(diffX) <= MoveSpeed * delta)
                {
                    if (snapPosition)
                    {
                        Position = new Vector2(targetX, Position.Y);
                        velocity.X = 0.0f;
                    }
                    else
                    {
                        velocity.X = diffX / delta;
                    }
                }
                else
                {
                    velocity.X = Mathf.Sign(diffX) * MoveSpeed;
                }
            }
        }
        // Moving HORIZONTALLY: Pull Y axis toward nearest grid box
        else if (inputDir.X != 0.0f)
        {
            float targetY = Mathf.Round(Position.Y / GRID_SIZE) * GRID_SIZE;
            float diffY = targetY - Position.Y;

            if (Mathf.Abs(diffY) > 0.01f)
            {
                if (Mathf.Abs(diffY) <= MoveSpeed * delta)
                {
                    if (snapPosition)
                    {
                        Position = new Vector2(Position.X, targetY);
                        velocity.Y = 0.0f;
                    }
                    else
                    {
                        velocity.Y = diffY / delta;
                    }
                }
                else
                {
                    velocity.Y = Mathf.Sign(diffY) * MoveSpeed;
                }
            }
        }

        return velocity;
    }

    private void UpdateFacingAndAnimation(Vector2 inputDir)
    {
        if (inputDir.X > 0)
        {
            _sprite.Animation = "walk_horizontal";
            _sprite.FlipH = false;
        }
        else if (inputDir.X < 0)
        {
            _sprite.Animation = "walk_horizontal";
            _sprite.FlipH = true;
        }
        else if (inputDir.Y > 0)
        {
            _sprite.Animation = "walk_down";
        }
        else if (inputDir.Y < 0)
        {
            _sprite.Animation = "walk_up";
        }
    }

    private void AdvanceAnimationFrame()
    {
        _movementFrameCounter++;
        if (_movementFrameCounter >= ANIMATION_STEP_FREQUENCY)
        {
            _movementFrameCounter = 0;
            int frameCount = _sprite.SpriteFrames.GetFrameCount(_sprite.Animation);
            if (frameCount > 0)
            {
                _sprite.Frame = (_sprite.Frame + 1) % frameCount;
            }
        }
    }
}