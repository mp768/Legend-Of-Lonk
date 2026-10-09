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

	// Null uses the stun state's own settings.
	public float? Duration { get; }
	public float? KnockbackSpeed { get; }

	// Whether the entity shows its hurt pose and flashes, as opposed to just freezing.
	public bool ShowHurt { get; }

	public StunCommand(Vector2 knockbackDirection, float? duration = null, float? knockbackSpeed = null, bool showHurt = true)
	{
		KnockbackDirection = knockbackDirection;
		Duration = duration;
		KnockbackSpeed = knockbackSpeed;
		ShowHurt = showHurt;
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

		stun.Configure(KnockbackDirection, Duration, KnockbackSpeed, ShowHurt);
		machine.PushState(stun);
		return true;
	}
}

public class CycleWeaponCommand : ICommand
{
	public bool Execute(Entity entity, PushdownStateMachine machine)
	{
		entity.CycleWeapon();
		return true;
	}
}

// Sends the entity charging in a direction, e.g. a blade trap that has spotted its target.
public class LungeCommand : ICommand
{
	public Vector2 Direction { get; }

	public LungeCommand(Vector2 direction)
	{
		Direction = direction;
	}

	public bool Execute(Entity entity, PushdownStateMachine machine)
	{
		if (machine.CurrentState is not MovementState)
		{
			return false;
		}

		var lunge = machine.GetState<LungeState>();
		if (lunge == null)
		{
			return false;
		}

		lunge.Configure(Direction);
		machine.PushState(lunge);
		return true;
	}
}

// Freezes the entity and carries it along with whatever grabbed it.
public class GrabCommand : ICommand
{
	public Node2D Carrier { get; }

	public GrabCommand(Node2D carrier)
	{
		Carrier = carrier;
	}

	public bool Execute(Entity entity, PushdownStateMachine machine)
	{
		if (machine.CurrentState is { IsInterruptable: false })
		{
			return false;
		}

		var grabbed = machine.GetState<GrabbedState>();
		if (grabbed == null)
		{
			return false;
		}

		grabbed.Configure(Carrier);
		machine.PushState(grabbed);
		return true;
	}
}
