using Godot;

public partial class Stalfos : CharacterBody2D, IResettableEntity
{
	[Export] private Area2D _hitBox;
	[Export] private SpritePresenter _spritePresenter;
    [Export] private PushdownStateMachine _stateMachine;
    [Export] private StalfoInputProvider _inputProvider;
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
				Vector2 facing = _inputProvider != null ? _inputProvider.FacingDirection : Vector2.Down;
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
