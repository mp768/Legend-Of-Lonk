using Godot;

public partial class Lonk : CharacterBody2D
{
	[Export] private GridMovementComponent _movementComponent;
	[Export] private GridAnimationComponent _animationComponent;
	[Export] private Area2D _hitBox;

	private enum Axis { None, Horizontal, Vertical }
	private Axis _primaryAxis = Axis.None;

	public override void _Ready()
	{
		// Hitbox will only trigger if it detects an item with the "cause-damage" layer mask.
		_hitBox.AreaEntered += OnAreaEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		float fDelta = (float)delta;
		Vector2 inputDir = GetCardinalInput(fDelta);

		_movementComponent?.Move(inputDir, delta);
		_animationComponent?.UpdateAnimation(inputDir);
	}

	private void OnAreaEntered(Area2D area)
	{
		// TODO: Implement health decrease here.
	}

	private Vector2 GetCardinalInput(float delta)
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

		if (hasY && hasX && _movementComponent != null)
		{
			if (_primaryAxis == Axis.Horizontal)
			{
				Vector2 horzVel = _movementComponent.CalculateGridAlignedVelocity(horzDir, delta, snapPosition: false);
				if (!TestMove(GlobalTransform, horzVel * delta))
				{
					return horzDir;
				}
				return vertDir;
			}
			else if (_primaryAxis == Axis.Vertical)
			{
				Vector2 vertVel = _movementComponent.CalculateGridAlignedVelocity(vertDir, delta, snapPosition: false);
				if (!TestMove(GlobalTransform, vertVel * delta))
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
