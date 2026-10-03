using System;
using Godot;

[GlobalClass]
public partial class GridMovementState : State
{
    [ExportGroup("Movement Settings")]
    [Export] public float MoveSpeed = 60.0f;
    [Export] public float GridSize = 8.0f;

    [ExportGroup("Animation Names & Pace")]
    [Export] public int StepFrequency = 6;
    [Export] public string AnimHorizontal = "walk_horizontal";
    [Export] public string AnimDown = "walk_down";
    [Export] public string AnimUp = "walk_up";

    private Vector2 _currentInputDir = Vector2.Zero;

	private Vector2 _prevInputDir;

    public void SetMoveDirection(Vector2 direction)
	{
		_currentInputDir = direction;

		if (direction != Vector2.Zero)
		{
			_prevInputDir = direction;
		}
	}
	public override void Resume()
	{
        Visuals?.StepDirectionalAnimation(_prevInputDir, AnimHorizontal, AnimDown, AnimUp, StepFrequency);
        Visuals?.UpdateSubpixelPosition();
	}

	public bool CheckDirection(Vector2 dir, double _delta)
	{
		Vector2 vel = CalculateGridAlignedVelocity(dir, (float)_delta, snapPosition: false);
		return !Entity.TestMove(Entity.GlobalTransform, vel * (float)_delta);
	}

    public override void PhysicsUpdate(double delta)
    {
        if (Entity == null) return;

        if (_currentInputDir != Vector2.Zero)
        {
            Entity.Velocity = CalculateGridAlignedVelocity(_currentInputDir, (float)delta, snapPosition: true);
        }
        else
        {
            Entity.Velocity = Vector2.Zero;
        }

        Entity.MoveAndSlide();

        // Delegate visual stepping and subpixel snapping to SpritePresenter
        Visuals?.StepDirectionalAnimation(_currentInputDir, AnimHorizontal, AnimDown, AnimUp, StepFrequency);
        Visuals?.UpdateSubpixelPosition();

        _currentInputDir = Vector2.Zero;
    }

    public Vector2 CalculateGridAlignedVelocity(Vector2 inputDir, float delta, bool snapPosition = true)
    {
        Vector2 velocity = inputDir * MoveSpeed;

        if (inputDir.Y != 0.0f)
        {
            float targetX = Mathf.Round(Entity.Position.X / GridSize) * GridSize;
            float diffX = targetX - Entity.Position.X;

            if (Mathf.Abs(diffX) > 0.01f)
            {
                if (Mathf.Abs(diffX) <= MoveSpeed * delta)
                {
                    if (snapPosition)
                    {
                        Entity.Position = new Vector2(targetX, Entity.Position.Y);
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
        else if (inputDir.X != 0.0f)
        {
            float targetY = Mathf.Round(Entity.Position.Y / GridSize) * GridSize;
            float diffY = targetY - Entity.Position.Y;

            if (Mathf.Abs(diffY) > 0.01f)
            {
                if (Mathf.Abs(diffY) <= MoveSpeed * delta)
                {
                    if (snapPosition)
                    {
                        Entity.Position = new Vector2(Entity.Position.X, targetY);
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
}