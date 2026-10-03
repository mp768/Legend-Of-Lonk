using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class PushdownStateMachine : Node
{
    [Export] public NodePath InitialStatePath;
    [Export] public NodePath EntityPath;
    [Export] public NodePath InputProviderPath;
    [Export] public NodePath SpritePresenterPath;

    private readonly Stack<State> _stateStack = new();
    private CharacterBody2D _entity;
    private IInputProvider _inputProvider;
    private SpritePresenter _spritePresenter;

    public State CurrentState => _stateStack.Count > 0 ? _stateStack.Peek() : null;

    public override void _Ready()
    {
        _entity = GetNode<CharacterBody2D>(EntityPath);
        _inputProvider = GetNode<IInputProvider>(InputProviderPath);
        _spritePresenter = SpritePresenterPath != null ? GetNode<SpritePresenter>(SpritePresenterPath) : null;

        foreach (Node child in GetChildren())
        {
            if (child is State state)
            {
                state.Initialize(this, _entity, _spritePresenter);
            }
        }

        if (InitialStatePath != null)
        {
            PushState(GetNode<State>(InitialStatePath));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        ICommand command = _inputProvider.FetchNextCommand();
        if (command != null)
        {
            command.Execute(_entity, this);
        }

        CurrentState?.PhysicsUpdate(delta);
    }

    public void PushState(State newState)
    {
        if (_stateStack.Count > 0) _stateStack.Peek().Pause();
        _stateStack.Push(newState);
        newState.Enter();
    }

    public void PopState()
    {
        if (_stateStack.Count == 0) return;

        State popped = _stateStack.Pop();
        popped.Exit();

        if (_stateStack.Count > 0) _stateStack.Peek().Resume();
    }
}