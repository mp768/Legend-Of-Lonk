using System.Reflection.Metadata;
using Godot;

public partial class Lonk : CharacterBody2D
{
	[Export] private GridMovementComponent _movementComponent;
	[Export] private GridAnimationComponent _animationComponent;
	[Export] private Area2D _hitBox;

	private enum Axis { None, Horizontal, Vertical }
	private Axis _primaryAxis = Axis.None;

	private Health lonk_hp;

	private const int UIHEALTH = 0;
	public override void _Ready()
	{
		lonk_hp = new Health(6);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, lonk_hp.health, UIHEALTH);

		// Connect cheat code signal
		GameSignals.Instance.SetCheatMode += setCheatMode;

		// Hitbox will only trigger if it detects an item with the "cause-damage" layer mask.
		_hitBox.AreaEntered += OnAreaEntered;
		_hitBox.BodyEntered += OnWeaponBodyEntered;
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
		if (area is Affectables affectable)
		{
			int amount = affectable.GetEffect();

			switch (affectable.GetEffectType())
			{
				case Affectables.Effects.DAMAGE:
				lonk_hp.applyHealthEffect(-amount);
				// TODO: Send link backwards from the way he's moving (or somehow make an enemy a "Weapon"?????)
				break;

				case Affectables.Effects.HEALTH:
				lonk_hp.applyHealthEffect(amount);
				break;

				case Affectables.Effects.RUPEES:
				GameState.Instance.gain_rupee(amount);
				break;

				case Affectables.Effects.KEYS:
				GameState.Instance.gain_key();
				break;


			}
		}
		
		
	}

	private void setCheatMode(bool set)
	{
		lonk_hp.HealthCheat(set);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, lonk_hp.health, UIHEALTH);
	}

	private void OnWeaponBodyEntered(Node node)
	{
		
		var collisionNode = node as CollisionObject2D;

		// TODO: Confirm it was a weapon we collided with, then pull its velocity to send the object backwards.
		if (node is CollisionObject2D body)
		{
			lonk_hp.applyHealthEffect((int)projectile.Get("damage"));
		}
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
