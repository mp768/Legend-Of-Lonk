using Godot;

public partial class Lonk : CharacterBody2D
{
	[Export] private GridMovementComponent _movementComponent;
	[Export] private GridAnimationComponent _animationComponent;
	[Export] private Area2D _hitBox;

	private enum Axis { None, Horizontal, Vertical }
	private Axis _primaryAxis = Axis.None;

	private Health health;

	private const int UIHEALTH = 0;
	public override void _Ready()
	{
		health = new Health(6);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);

		// Connect cheat code signal
		GameSignals.Instance.SetCheatMode += setCheatMode;

		// Hitbox will only trigger if it detects an item with the "cause-damage" layer mask.
		_hitBox.AreaEntered += OnAreaEntered;
	}

	// Player has no control for this.
	private bool _noInput = false;
	private Vector2 _previousDirection = Vector2.Zero;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 inputDir = GetCardinalInput(delta);

		if (inputDir != Vector2.Zero) {
			_previousDirection = inputDir;
		}

		_movementComponent?.Move(inputDir, delta);
		_animationComponent?.UpdateAnimation(inputDir);
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is Affectables affectable)
		{
			switch (affectable.Type)
			{
				case Affectables.EffectType.DAMAGE:
					health.applyHealthEffect(-affectable.Value);
					GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);

					var direction = affectable.Direction ?? -_previousDirection;

					_movementComponent?.ApplyForce(direction, 0.25f);
					_animationComponent?.StartColorFluctuation(0.25f);

					// TODO: Send link backwards from the way he's moving (or somehow make an enemy a "Weapon"?????)
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

	private void setCheatMode(bool set)
	{
		health.HealthCheat(set);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);
	}

	private Vector2 GetCardinalInput(double delta)
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
				Vector2 horzVel = _movementComponent.CalculateGridAlignedVelocity(horzDir, (float)delta, snapPosition: false);
				if (!TestMove(GlobalTransform, horzVel * (float)delta))
				{
					return horzDir;
				}
				return vertDir;
			}
			else if (_primaryAxis == Axis.Vertical)
			{
				Vector2 vertVel = _movementComponent.CalculateGridAlignedVelocity(vertDir, (float)delta, snapPosition: false);
				if (!TestMove(GlobalTransform, vertVel * (float)delta))
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
