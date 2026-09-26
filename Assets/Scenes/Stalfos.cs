using Godot;

public partial class Stalfos : CharacterBody2D, IResettableEntity
{
	[Export] private GridMovementComponent _movementComponent;
	[Export] private GridAnimationComponent _animationComponent;
	[Export] private Area2D _hitBox;

	private Vector2[] directionMapping =
	[
		new(-1, 0),
		new(1, 0),
		new(0, 1),
		new(0, -1),
	];

	private int currentDirectionIndex = 0;

	private Health hp;

	public override void _Ready()
	{
		hp = new Health(6);

		// Hitbox will only trigger if it detects an item with the "cause-damage" layer mask.
		_hitBox.AreaEntered += OnAreaEntered;
		_hitBox.BodyEntered += OnWeaponBodyEntered;

		var timer = new Timer()
		{
			WaitTime = 0.48f,
			Autostart = true,
		};

		timer.Timeout += ChangeDirection;

		AddChild(timer);

		initialPosition = GlobalPosition;
	}

	public Vector2 initialPosition;

	public void OnRoomEntered()
	{
		GlobalPosition = initialPosition;

		Visible = true;
	}

	public async void OnRoomExited()
	{
		// await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);

		// Visible = false;
	}

	private void ChangeDirection()
	{
		currentDirectionIndex = (int)(GD.Randi() % directionMapping.Length);
	}

	public override void _PhysicsProcess(double delta)
	{
		var inputDir = directionMapping[currentDirectionIndex];

		_movementComponent?.Move(inputDir, delta);
		_animationComponent?.UpdateAnimation(inputDir);
	}

	private void OnAreaEntered(Area2D area)
	{
		// TODO: Implement health decrease here.
		// TODO: Send link backwards from the way he's moving (or somehow make an enemy a "Weapon"?????)
	}

	private void OnWeaponBodyEntered(Node node)
	{
		// TODO: Confirm it was a weapon we collided with, then pull its velocity to send the object backwards.
	}
}
