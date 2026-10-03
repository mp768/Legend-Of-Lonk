using Godot;

public interface ICommand
{
    // Returns true if the command was successfully handled/executed
    bool Execute(CharacterBody2D entity, PushdownStateMachine machine);
}

// 1. Move Command
public class MoveCommand : ICommand
{
    public Vector2 Direction { get; }

    public MoveCommand(Vector2 direction)
    {
        Direction = direction;
    }

    public bool Execute(CharacterBody2D entity, PushdownStateMachine machine)
    {
        if (machine.CurrentState is GridMovementState moveState)
        {
            moveState.SetMoveDirection(Direction);
            return true;
        }
        return false;
    }
}

// 2. Attack Command

public class AttackCommand : ICommand
{
    private readonly NodePath _attackStatePath;
    private readonly int _frameIndex;
    private readonly Vector2 _direction;

    public AttackCommand(NodePath attackStatePath, int frameIndex, Vector2 direction)
    {
        _attackStatePath = attackStatePath;
        _frameIndex = frameIndex;
        _direction = direction;
    }

    public bool Execute(CharacterBody2D entity, PushdownStateMachine machine)
    {
        // Only trigger attack if entity is currently in GridMovementState
        if (machine.CurrentState is GridMovementState)
        {
            var attackState = machine.GetNodeOrNull<AttackState>(_attackStatePath);
            if (attackState != null)
            {
                attackState.SetupAttack(_frameIndex, _direction);
                machine.PushState(attackState);
                return true;
            }
        }
        return false;
    }
}

// 3. Stun Command (Applies to both Player & Enemy)
public class StunCommand : ICommand
{
    private readonly StunState _stunState;
    private readonly float _duration;
    private readonly Vector2 _direction;
    private readonly float _speed;
    private readonly float _delayOffset;

    public StunCommand(
        StunState stunState, 
        float duration = -1f, 
        Vector2 direction = default, 
        float speed = -1f, 
        float delayOffset = -1f)
    {
        this._stunState = stunState;
        _duration = duration;
        _direction = direction;
        _speed = speed;
        _delayOffset = delayOffset;
    }

    public bool Execute(CharacterBody2D entity, PushdownStateMachine machine)
    {
        if (_stunState != null)
        {
            _stunState.ApplyStun(_duration, _direction, _speed, _delayOffset);
            return true;
        }
        return false;
    }
}