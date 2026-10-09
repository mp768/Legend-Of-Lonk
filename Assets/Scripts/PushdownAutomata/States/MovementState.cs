using Godot;

// Base for states that move an entity from directional input. MoveCommand targets this type, so any style
// of movement (grid walking, free flight, ...) works with any input provider. Attacks start from these states.
public abstract partial class MovementState : State
{
    [Export] public float MoveSpeed { get; set; } = 60f;

    // The desired direction for the next tick. Each axis is -1, 0 or 1, and both may be set at once.
    // Some movement styles also accept shorter vectors to move more slowly.
    public abstract void SetMoveInput(Vector2 input);

    // Whatever is pushed on top isn't moving the entity, so it no longer intends to move.
    public override void Pause()
    {
        Entity.MoveIntent = Vector2.Zero;
    }

    public override void Exit()
    {
        Entity.MoveIntent = Vector2.Zero;
    }
}
