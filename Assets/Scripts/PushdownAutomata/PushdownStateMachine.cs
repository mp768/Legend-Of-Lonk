using Godot;
using System.Collections.Generic;

// Each physics tick, fetches a command from the current input provider, executes it, then updates the
// state on top of the stack. Its State children are the states available to the entity.
[GlobalClass]
public partial class PushdownStateMachine : Node
{
    [Export] private State initialState;

    // Must implement IInputProvider. Typed as Node because Godot can't export interfaces.
    [Export] private Node initialInputProvider;

    public Entity Entity { get; private set; }
    public IInputProvider InputProvider { get; set; }
    public State CurrentState => stack.Count > 0 ? stack.Peek() : null;

    private readonly Stack<State> stack = new();
    private readonly List<State> states = [];

    // Called by the owning entity once its own references are ready.
    public void Start(Entity entity)
    {
        Entity = entity;

        InputProvider = initialInputProvider as IInputProvider;
        if (initialInputProvider != null && InputProvider == null)
        {
            GD.PushError($"{initialInputProvider.Name} does not implement {nameof(IInputProvider)}.");
        }

        foreach (Node child in GetChildren())
        {
            if (child is State state)
            {
                state.Initialize(this);
                states.Add(state);
            }
        }

        if (initialState != null)
        {
            PushState(initialState);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Entity == null)
        {
            return;
        }

        InputProvider?.FetchNextCommand()?.Execute(Entity, this);
        CurrentState?.PhysicsUpdate(delta);
    }

    // Returns the first state of the given type, or null if this entity doesn't have one.
    public T GetState<T>() where T : State
    {
        foreach (State state in states)
        {
            if (state is T match)
            {
                return match;
            }
        }
        return null;
    }

    public void PushState(State newState)
    {
        CurrentState?.Pause();
        stack.Push(newState);
        newState.Enter();
    }

    public void PopState()
    {
        if (stack.Count == 0)
        {
            return;
        }

        stack.Pop().Exit();
        CurrentState?.Resume();
    }
}
