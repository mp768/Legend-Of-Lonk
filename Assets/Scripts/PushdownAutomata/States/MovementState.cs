using Godot;

// Base for states that move an entity from directional input. MoveCommand targets this type, so any style
// of movement (grid walking, free flight, ...) works with any input provider. Attacks start from these states.
public abstract partial class MovementState : State
{
    [Export] public float MoveSpeed { get; set; } = 60f;

    // The desired direction for the next tick. Each axis is -1, 0 or 1, and both may be set at once.
    public abstract void SetMoveInput(Vector2 input);
}
