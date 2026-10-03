using Godot;
using System;

[GlobalClass]
public partial class TransitionState : State
{
    [ExportGroup("Animation Names & Pace")]
    [Export] public int StepFrequency = 6;
    [Export] public string AnimHorizontal = "walk_horizontal";
    [Export] public string AnimDown = "walk_down";
    [Export] public string AnimUp = "walk_up";

    private Vector2 _currentInputDir = Vector2.Zero;

    public void SetMoveDirection(Vector2 direction)
	{
		_currentInputDir = direction;
	}

    public override void PhysicsUpdate(double delta)
    {
        if (Entity == null) return;

        // Delegate visual stepping and subpixel snapping to SpritePresenter
        Visuals?.StepDirectionalAnimation(_currentInputDir, AnimHorizontal, AnimDown, AnimUp, StepFrequency);
        Visuals?.UpdateSubpixelPosition();
    }
}
