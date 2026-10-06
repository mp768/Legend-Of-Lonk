using Godot;

public partial class Stalfos : CharacterBody2D, IResettableEntity
{
	[Export] private Area2D _hitBox;
	[Export] private SpritePresenter _spritePresenter;
	[Export] private PushdownStateMachine _stateMachine;
	private IInputProvider _inputProvider;
	[Export] private PlayerInputProvider _playerInputProvider;
	[Export] private StalfoInputProvider _stalfosInputProvider;
	[Export] private StunState _stunState;

	private Health health;

	private const int UIHEALTH = 0;

	public override void _Ready()
	{
		health = new Health(6);

		// Hitbox will only trigger if it detects an item with the "cause-damage" layer mask.
		_hitBox.AreaEntered += OnAreaEntered;

		health.WhenZero += Erase;

		initialPosition = GlobalPosition;

		_inputProvider = _stalfosInputProvider;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key)
		{
			if (key.IsActionPressed("swap"))
			{
				if (_inputProvider == _stalfosInputProvider)
				{
					_inputProvider = _playerInputProvider;
				} 
				else
				{
					_inputProvider = _stalfosInputProvider;
				}
				
				_stateMachine.InputProvider = _inputProvider;
			}
		}
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

	private async void OnAreaEntered(Area2D area)
	{
		if (area is Affectables affectable)
		{
			if (affectable.Type is Affectables.EffectType.DAMAGE)
			{
				health.applyHealthEffect(-affectable.Value);

				// Calculate knockback direction relative to player facing direction or damage source
				Vector2 facing = Vector2.Down;
				if (_inputProvider != null)
				{
					if (_inputProvider is StalfoInputProvider)
					{
						facing = _stalfosInputProvider.FacingDirection;
					}
					else if (_inputProvider is PlayerInputProvider)
					{
						facing = _playerInputProvider.FacingDirection;
					}
				}

				Vector2 knockbackDir = affectable.Direction ?? -facing;

				var stunCmd = new StunCommand(_stunState, duration: 0.25f, direction: knockbackDir, speed: 120f);
				stunCmd.Execute(this, _stateMachine);
			}
		}
		
	}
	
	private void Erase()
	{
		QueueFree();
	}
}
