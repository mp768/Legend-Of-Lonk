using Godot;
using System;

public partial class Lonk : CharacterBody2D
{
    [Export] private GridMovementComponent _movementComponent;
    [Export] private Vector2I spriteOffset;
    [Export] private AnimatedSprite2D _sprite;

    private int _movementFrameCounter = 0;
    private const int ANIMATION_STEP_FREQUENCY = 6;

    private enum Axis { None, Horizontal, Vertical }
    private Axis _primaryAxis = Axis.None;

    public override void _Ready()
    {
        if (_sprite != null)
        {
            _sprite.Stop(); 
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float fDelta = (float)delta;
        Vector2 inputDir = GetCardinalInput(fDelta);

        // Delegate the actual movement & snapping to the component
        _movementComponent.Move(inputDir, delta);

        if (inputDir != Vector2.Zero)
        {
            UpdateFacingAndAnimation(inputDir);
            AdvanceAnimationFrame();
        }
        else
        {
            _movementFrameCounter = 0;
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

        if (hasY && hasX)
        {
            if (_primaryAxis == Axis.Horizontal)
            {
                Vector2 horzVel = _movementComponent.CalculateGridAlignedVelocity(horzDir, delta, snapPosition: false);
                if (!TestMove(GlobalTransform, horzVel * delta))
                {
                    return horzDir;
                }
                return vertDir; 
            }
            else if (_primaryAxis == Axis.Vertical)
            {
                Vector2 vertVel = _movementComponent.CalculateGridAlignedVelocity(vertDir, delta, snapPosition: false);
                if (!TestMove(GlobalTransform, vertVel * delta))
                {
                    return vertDir;
                }
                return horzDir; 
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