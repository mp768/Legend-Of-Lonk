using Godot;

[GlobalClass]
public partial class EnemyInputProvider : Node, IInputProvider
{
    [Export] public NodePath AttackStatePath;
    
    private ICommand _queuedCommand = null;

    public void QueueAIAction(ICommand command)
    {
        _queuedCommand = command;
    }

    public ICommand FetchNextCommand()
    {
        ICommand cmd = _queuedCommand;
        _queuedCommand = null; // Consume command
        return cmd;
    }
}