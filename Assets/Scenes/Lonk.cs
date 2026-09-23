using Godot;
using System;

public partial class Lonk : Area2D
{
	private enum MovementDirection
	{
		HORIZONTAL,
		VERTICAL,
	}

	private Vector2I RAY_LENGTH = new(6, 3);

	private AnimatedSprite2D sprite;
	private Vector2 footPostitionOffset;

	private MovementDirection previousMovementDirection = MovementDirection.HORIZONTAL;

	// The position we do movement with. The "Position" field on the native Node2D is just
	// going to be used for visuals.
	private Vector2 actualPosition;

	// I recorded how many frames it took for Link's movement sprite to update and it
	// was every 6 movement frames.
	private const int MOVEMENT_SUB_ANIMATION_FRAME_MAX = 6;
	private int movementSubAnimationFrame = 0;

	public override void _Ready()
	{
		sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		actualPosition = Position;

		var footPositionNode = GetNode<Node2D>("Foot Position");
		footPostitionOffset = footPositionNode.Position;

		// We don't need this marker to live after we've gotten this information.
		footPositionNode.QueueFree();
	}

	private Vector2I GetInputDirection()
	{
		return new Vector2I(
			(int)(Input.GetActionStrength("right") - Input.GetActionStrength("left")),
			(int)(Input.GetActionStrength("down") - Input.GetActionStrength("up"))
		);
	}

	private (bool, bool) GetDirectionRaycasts(Vector2I direction)
	{
		var spaceState = GetWorld2D().DirectSpaceState;

		var position = GlobalPosition + footPostitionOffset;

		var verticalQuery = PhysicsRayQueryParameters2D.Create(
			position,
			position + new Vector2(0, direction.Y * RAY_LENGTH.Y)
		);

		var horizontalQuery = PhysicsRayQueryParameters2D.Create(
			position,
			position + new Vector2(direction.X * RAY_LENGTH.X, 0)
		);

		var verticalResult = spaceState.IntersectRay(verticalQuery);
		var horizontalResult = spaceState.IntersectRay(horizontalQuery);

		var hitVertical = false;
		var hitHorizontal = false;

		if (verticalResult.Count > 0)
		{
			var vCollider = verticalResult["collider"].As<Node>();

			if (vCollider != null)
				hitVertical = vCollider.IsInGroup(Constants.COLLIDING_OBJECT_GROUP_TAG);
		}

		if (horizontalResult.Count > 0)
		{
			var hCollider = horizontalResult["collider"].As<Node>();

			if (hCollider != null)
				hitHorizontal = hCollider.IsInGroup(Constants.COLLIDING_OBJECT_GROUP_TAG);
		}

		return (hitHorizontal, hitVertical);
	}

	private void RoundPositionToTile()
	{
		// Visual tiles on the NES were 8x8, while logical tiles for blocks of sprites
		// were typically 16x16. However, in the LoZ if link changed to a different 
		// axis of movement, he would always bind himself to the nearest visual tile,
		// so 8x8.

		GlobalPosition = new Vector2(
			Mathf.Round((GlobalPosition.X + 1) / 8.0f) * 8.0f,
			Mathf.Round((GlobalPosition.Y + 1) / 8.0f) * 8.0f
		);
	}

	public override void _PhysicsProcess(double delta)
	{
		var inputDirection = GetInputDirection();

		var (hHit, vHit) = GetDirectionRaycasts(inputDirection);

		// Vertical movement has priority until Link is stuck hitting
		// a wall.
		if (!vHit && inputDirection.Y != 0)
		{
			// In the original game, when you switch your axis of movement,
			// Link's position gets locked to the center of a nearby
			// 8x8 tile.
			if (previousMovementDirection != MovementDirection.VERTICAL)
			{
				sprite.Frame = 0;
				RoundPositionToTile();
				previousMovementDirection = MovementDirection.VERTICAL;
			}

			// Reminder: Positive is down in Godot.
			if (inputDirection.Y > 0)
			{
				sprite.Animation = "walk_down";
			}
			else
			{
				sprite.Animation = "walk_up";
			}

			actualPosition += new Vector2(0, inputDirection.Y * 1.5f);
		}
		else if (!hHit && inputDirection.X != 0)
		{
			if (previousMovementDirection != MovementDirection.HORIZONTAL)
			{
				sprite.Frame = 0;
				RoundPositionToTile();
				previousMovementDirection = MovementDirection.HORIZONTAL;
			}

			sprite.Animation = "walk_horizontal";
			sprite.FlipH = inputDirection.X < 0;

			actualPosition += new Vector2(inputDirection.X * 1.5f, 0);
		}

		Position = new Vector2I((int)actualPosition.X, (int)actualPosition.Y);
	}
}
