using Godot;

// One behaviour on an entity's state stack. Pause and Resume run when another state is pushed on top of
// this one and when that state is popped off again.
public abstract partial class State : Node
{
    protected PushdownStateMachine Machine { get; private set; }
    protected Entity Entity => Machine.Entity;
    protected SpritePresenter Visuals => Entity.Visuals;

    // Whether being hit may push a stun on top of this state.
    public virtual bool IsInterruptable => true;

    public virtual void Initialize(PushdownStateMachine machine)
    {
        Machine = machine;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Pause() { }
    public virtual void Resume() { }
    public virtual void PhysicsUpdate(double delta) { }
}
