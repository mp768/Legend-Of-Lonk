using Godot;

[GlobalClass]
public partial class PushableBlock : AnimatableBody2D
{
	[Signal]
	public delegate void PushedEventHandler();

	[Export] private Area2D pushSensor;

	[Export] public float PushDelay { get; set; } = 0.5f;

	[Export] public float SlideDuration { get; set; } = 0.25f;

	[Export] public Vector2 AllowedDirection { get; set; } = Vector2.Zero;

	private float pushTime;
	private bool moved;
	private Tween slideTween;

	public override void _PhysicsProcess(double delta)
	{
		if (moved)
		{
			return;
		}

		Vector2? push = FindPush();
		if (push == null)
		{
			pushTime = 0f;
			return;
		}

		pushTime += (float)delta;
		if (pushTime >= PushDelay)
		{
			TrySlide(push.Value);
		}
	}

	public override void _ExitTree()
	{
		if (slideTween != null && slideTween.IsValid())
		{
			slideTween.Kill();
		}
	}

	// The direction the block is being pushed in, or null if nobody is pushing it.
	private Vector2? FindPush()
	{
		foreach (Node2D body in pushSensor.GetOverlappingBodies())
		{
			if (body is not Entity pusher || !pusher.IsInGroup(Constants.PLAYER_GROUP))
			{
				continue;
			}

			Vector2 intent = pusher.MoveIntent;
			if (intent == Vector2.Zero || (AllowedDirection != Vector2.Zero && intent != AllowedDirection))
			{
				continue;
			}

			// Makes it so the pusher has to be walking toward the block and actually pressed against it.
			Vector2 toBlock = (GlobalPosition - pusher.GlobalPosition).Normalized();
			if (intent.Dot(toBlock) > 0.7f && pusher.TestMove(pusher.GlobalTransform, intent))
			{
				return intent;
			}
		}

		return null;
	}

	private void TrySlide(Vector2 direction)
	{
		Vector2 offset = direction * Constants.TILE_SIZE;
		if (TestMove(GlobalTransform, offset))
		{
			pushTime = 0f;
			return;
		}

		moved = true;
		slideTween = CreateTween();
		slideTween.TweenProperty(this, Node2D.PropertyName.Position.ToString(), Position + offset, SlideDuration);
		EmitSignal(SignalName.Pushed);
	}
}
