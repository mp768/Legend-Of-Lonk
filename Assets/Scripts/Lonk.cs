using Godot;

public partial class Lonk : CharacterBody2D
{
    [Export] private Area2D _hitBox;
	[Export] private SpritePresenter _spritePresenter;
    [Export] private PushdownStateMachine _stateMachine;
    [Export] private PlayerInputProvider _inputProvider;
    [Export] private StunState _stunState;
	[Export] private TransitionState _transitionState;

    private Health health;
    private const int UIHEALTH = 0;

	private Timer _invincibilityTimer; 

	[Export] private float iFrames;
	private bool _takeDamage = true;

    public override void _Ready()
    {
        health = new Health(6);

        GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);
        health.WhenZero += GameOver;

        // Connect cheat code signal
        GameSignals.Instance.SetCheatMode += SetCheatMode;

        // Hitbox triggers
        if (_hitBox != null)
        {
            _hitBox.AreaEntered += OnAreaEntered;
        }

		GameSignals.Instance.TransitionOccur += TransitionOccur;

		_invincibilityTimer = new Timer
       	{
           WaitTime = iFrames,
           Autostart = false
       	};

		_invincibilityTimer.Timeout += () => _takeDamage = true;

		AddChild(_invincibilityTimer);
    }

	private void TransitionOccur(bool start, Vector2 mv)
	{
		if (start)
		{
			_transitionState.SetMoveDirection(mv); 
			_stateMachine.PushState(_transitionState);
		}
		else
		{
			_stateMachine.PopState();
		}
	}

    private void OnAreaEntered(Area2D area)
    {
        if (area is Affectables affectable)
        {
            switch (affectable.Type)
            {
                case Affectables.EffectType.DAMAGE:

					if (!_takeDamage) break;

					_takeDamage = false;
					health.applyHealthEffect(-affectable.Value);
	                GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);	
					_invincibilityTimer.Start();

                    // Calculate knockback direction relative to player facing direction or damage source
                    Vector2 facing = _inputProvider != null ? _inputProvider.FacingDirection : Vector2.Down;
                    Vector2 knockbackDir = affectable.Direction ?? -facing;

                    // Push StunCommand to interrupt whatever state Lonk is currently in
                    if (_stateMachine.CurrentState.IsInterruptable)
					{
						var stunCmd = new StunCommand(_stunState, duration: iFrames, direction: knockbackDir, speed: 120f);
                    	stunCmd.Execute(this, _stateMachine);	
					}

					_spritePresenter.StartColorFluctuation(stepDuration: iFrames);

                    break;
                
                case Affectables.EffectType.HEALTH:
                    health.applyHealthEffect(affectable.Value);
                    GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);
                    break;

                case Affectables.EffectType.RUPEES:
                    GameState.Instance.gain_rupee(affectable.Value);
                    break;

                case Affectables.EffectType.KEYS:
                    GameState.Instance.gain_key();
                    break;
            }
        }
    }

    private void SetCheatMode(bool set)
    {
        health.HealthCheat(set);
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);
    }

    private void GameOver()
    {
        GameState.Instance.resetGameState();
    }
}