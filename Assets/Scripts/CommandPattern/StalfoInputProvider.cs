using Godot;

[GlobalClass]
public partial class StalfoInputProvider : Node, IInputProvider
{
	private int currentDirectionIndex = 0;
	private Vector2 _facingDirection = Vector2.Down;
	public Vector2 FacingDirection => _facingDirection;

	private ICommand[] directionMapping = 
	[
		new MoveCommand(new(-1, 0)),	
		new MoveCommand(new(1, 0)),	
		new MoveCommand(new(0, 1)),	
		new MoveCommand(new(0, -1)),	
	];

	private Vector2[] directionMappingVectors = 
	[
		new(-1, 0),	
		new(1, 0),	
		new(0, 1),	
		new(0, -1),	
	];

    public override void _Ready()
    {
		var timer = new Timer()
		{
			WaitTime = 0.78f,
			Autostart = true,
		};

		timer.Timeout += ChangeDirection;

		AddChild(timer);
    }

	private void ChangeDirection()
	{
		currentDirectionIndex = (int)(GD.Randi() % directionMapping.Length);
		_facingDirection = directionMappingVectors[currentDirectionIndex];
	}

    public ICommand FetchNextCommand()
    {
        return directionMapping[currentDirectionIndex];
    }
}