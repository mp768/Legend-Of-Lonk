using Godot;

public abstract partial class State : Node
{
    protected PushdownStateMachine Machine { get; private set; }
    protected CharacterBody2D Entity { get; private set; }
    protected SpritePresenter Visuals { get; private set; }
    public bool IsInterruptable { get; protected set; } = true;

    public virtual void Initialize(PushdownStateMachine machine, CharacterBody2D entity, SpritePresenter presenter)
    {
        Machine = machine;
        Entity = entity;
        Visuals = presenter;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Pause() { }
    public virtual void Resume() { }

    public virtual void Update(double delta) { }
    public virtual void PhysicsUpdate(double delta) { }
}