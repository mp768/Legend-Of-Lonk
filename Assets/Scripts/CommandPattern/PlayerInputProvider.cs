using System.Reflection.Metadata;
using Godot;

[GlobalClass]
public partial class PlayerInputProvider : Node, IInputProvider
{
    [Export] public NodePath MachinePath;
    [Export] public NodePath AttackStatePath;

	[Export] public NodePath MovementStatePath;

    private enum Axis { None, Horizontal, Vertical }
    private Axis _primaryAxis = Axis.None;
    private Vector2 _facingDirection = Vector2.Down;

    public Vector2 FacingDirection => _facingDirection;

	private GridMovementState _movementState;

	private double _delta;

    public override void _PhysicsProcess(double delta)
    {
        this._delta = delta;
    }

    public override void _Ready()
    {
        _movementState = GetNode<GridMovementState>(MovementStatePath);
    }

    public ICommand FetchNextCommand()
    {
        // 1. Attack Inputs
        if (Input.IsActionJustPressed("standard_attack"))
        {
            return new AttackCommand(AttackStatePath, frameIndex: 1, _facingDirection); // Sword Attack
        }
        if (Input.IsActionJustPressed("alternate_attack"))
        {
            return new AttackCommand(AttackStatePath, frameIndex: 0, _facingDirection); // Bow / Item Throw
        }

        // 2. Cardinal Grid Movement Inputs
        Vector2 inputDir = GetCardinalInput();
        if (inputDir != Vector2.Zero)
        {
            _facingDirection = inputDir;
            return new MoveCommand(inputDir);
        }

        return new MoveCommand(Vector2.Zero);
    }

    private Vector2 GetCardinalInput()
	{
		float x = Input.GetAxis("left", "right");
		float y = Input.GetAxis("up", "down");

		bool hasX = !Mathf.IsZeroApprox(x);
		bool hasY = !Mathf.IsZeroApprox(y);

		if (!hasX && !hasY)
		{
			_primaryAxis = Axis.None;
			return Vector2.Zero;
		}

		Vector2 vertDir = hasY ? new Vector2(0.0f, Mathf.Sign(y)) : Vector2.Zero;
		Vector2 horzDir = hasX ? new Vector2(Mathf.Sign(x), 0.0f) : Vector2.Zero;

		if (hasY && hasX && _movementState != null)
		{
			if (_primaryAxis == Axis.Horizontal)
			{
				if (_movementState.CheckDirection(horzDir, _delta))
				{
					return horzDir;
				}
				return vertDir;
			}
			else if (_primaryAxis == Axis.Vertical)
			{
				if (_movementState.CheckDirection(vertDir, _delta))
				{
					return vertDir;
				}
				return horzDir;
			}
		}

		if (hasY)
		{
			_primaryAxis = Axis.Vertical;
			return vertDir;
		}

		_primaryAxis = Axis.Horizontal;
		return horzDir;
	}

}