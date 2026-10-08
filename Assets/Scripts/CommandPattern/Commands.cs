using Godot;

// A command decides what an input means for the entity right now: feed the current state, push a new
// one, or do nothing. Commands find their target states through the machine, so the same input works
// for any entity that has those states.
public interface ICommand
{
	// Returns true if the command was handled.
	bool Execute(Entity entity, PushdownStateMachine machine);
}

public class MoveCommand : ICommand
{
	public Vector2 Direction { get; }

	public MoveCommand(Vector2 direction)
	{
		Direction = direction;
	}

	public bool Execute(Entity entity, PushdownStateMachine machine)
	{
		if (machine.CurrentState is not MovementState movement)
		{
			return false;
		}

		movement.SetMoveInput(Direction);
		return true;
	}
}

public class AttackCommand : ICommand
{
	public AttackState.AttackKind Kind { get; }

	public AttackCommand(AttackState.AttackKind kind)
	{
		Kind = kind;
	}

	public bool Execute(Entity entity, PushdownStateMachine machine)
	{
		if (machine.CurrentState is not MovementState)
		{
			return false;
		}

		var attack = machine.GetState<AttackState>();
		if (attack == null)
		{
			return false;
		}

		attack.Configure(Kind);
		machine.PushState(attack);
		return true;
	}
}

public class StunCommand : ICommand
{
	public Vector2 KnockbackDirection { get; }

	public StunCommand(Vector2 knockbackDirection)
	{
		KnockbackDirection = knockbackDirection;
	}

	public bool Execute(Entity entity, PushdownStateMachine machine)
	{
		if (machine.CurrentState is { IsInterruptable: false })
		{
			return false;
		}

		var stun = machine.GetState<StunState>();
		if (stun == null)
		{
			return false;
		}

		stun.Configure(KnockbackDirection);
		machine.PushState(stun);
		return true;
	}
}
