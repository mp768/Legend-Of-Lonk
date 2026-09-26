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

	private Health health;

	private const int UIHEALTH = 0;

	public override void _Ready()
	{
		health = new Health(6);

		// Hitbox will only trigger if it detects an item with the "cause-damage" layer mask.
		_hitBox.AreaEntered += OnAreaEntered;

		var timer = new Timer()
		{
			WaitTime = 0.78f,
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

	private async void OnAreaEntered(Area2D area)
	{
		if (area is Affectables affectable)
		{
			if (affectable.Type is Affectables.EffectType.DAMAGE)
			{
				health.applyHealthEffect(-affectable.Value);

				var direction = affectable.Direction ?? -directionMapping[currentDirectionIndex];

				var initialStepFrequency = _animationComponent.stepFrequency;
				_animationComponent.stepFrequency = 3;

				_movementComponent?.ApplyForce(direction, 0.85f, 5000.5f);
				_animationComponent?.StartColorFluctuation(0.85f);


				await ToSignal(GetTree().CreateTimer(0.825f), SceneTreeTimer.SignalName.Timeout);

				_animationComponent.stepFrequency = initialStepFrequency;
			}
		}
		
	}
}
