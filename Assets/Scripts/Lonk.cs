using Godot;

public partial class Lonk : CharacterBody2D
{
	[Export] private GridMovementComponent _movementComponent;
	[Export] private GridAnimationComponent _animationComponent;
	[Export] private Area2D _hitBox;

	[Export] private Affectables _swordLeft;
	[Export] private Affectables _swordRight;
	[Export] private Affectables _swordDown;
	[Export] private Affectables _swordUp;

	private enum Axis { None, Horizontal, Vertical }
	private Axis _primaryAxis = Axis.None;

	private enum WeaponType { Sword, Bow }
	private WeaponType _currentWeapon = WeaponType.Sword;

	private Vector2 _previousDirection = Vector2.Down; // Default facing direction

	private Health health;

	private const int UIHEALTH = 0;

	// Attack state tracking
	private bool _isAttacking = false;
	private int _attackSessionId = 0;

	public override void _Ready()
	{
		health = new Health(6);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);

		_swordLeft.Direction = new(-1, 0);
		_swordRight.Direction = new(1, 0);
		_swordUp.Direction = new(0, -1);
		_swordDown.Direction = new(0, 1);

		// Ensure all sword hitboxes start disabled
		DisableAllSwords();

		// Connect cheat code signal
		GameSignals.Instance.SetCheatMode += setCheatMode;

		// Hitbox will only trigger if it detects an item with the "cause-damage" layer mask.
		_hitBox.AreaEntered += OnAreaEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Handle attacks when not currently mid-attack
		if (!_isAttacking)
		{
			if (Input.IsActionJustPressed("standard_attack"))
			{
				TriggerAttack(1); // Sword (Frame 1)
			}
			else if (Input.IsActionJustPressed("alternate_attack"))
			{
				TriggerAttack(0); // Bow / Item Throw (Frame 0)
			}
		}

		if (_isAttacking)
		{
			_movementComponent?.Move(Vector2.Zero, delta);
			return;
		}

		Vector2 inputDir = GetCardinalInput(delta);

		if (inputDir != Vector2.Zero) {
			_previousDirection = inputDir;
		}

		_movementComponent?.Move(inputDir, delta);
		_animationComponent?.UpdateAnimation(inputDir);
	}

	private async void TriggerAttack(int frameIndex)
	{
		_isAttacking = true;
		int currentSession = ++_attackSessionId;

		// Resolve facing direction (defaults to Down if stationary at start)
		Vector2 dir = _previousDirection == Vector2.Zero ? Vector2.Down : _previousDirection;

		string animName = "sword_down";
		string stopAnimName = "walk_down";
		Vector2I spriteOffset = new(0, -2);
		bool flipH = false;

		Affectables activeSword = null;

		if (dir.X > 0)
		{
			animName = "sword_horizontal";
			stopAnimName = "walk_horizontal";
			flipH = false;

			if (frameIndex == 1)
			{
				spriteOffset = new(6, -2);  
				activeSword = _swordRight;
			}
		}
		else if (dir.X < 0)
		{
			animName = "sword_horizontal";
			stopAnimName = "walk_horizontal";
			flipH = true;

			if (frameIndex == 1)
			{
				spriteOffset = new(-6, -2); 
				activeSword = _swordLeft;
			}
		}
		else if (dir.Y < 0)
		{
			animName = "sword_up";
			stopAnimName = "walk_up";
			spriteOffset = new(0, -10);

			if (frameIndex == 1) {
				activeSword = _swordUp;
			}
		}
		else if (dir.Y > 0)
		{
			animName = "sword_down";
			stopAnimName = "walk_down";
			spriteOffset = new(0, 5);

			if (frameIndex == 1) {
				activeSword = _swordDown;
			}
		}

		if (activeSword != null)
		{
			EnableSword(activeSword);
		}

		// Play single attack frame
		_animationComponent?.SetAnimationAndFrame(animName, frameIndex, spriteOffset, flipH);

		// Lock movement for 0.5s
		await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);

		if (IsInstanceValid(this) && currentSession == _attackSessionId)
		{
			_isAttacking = false;
			_animationComponent?.SetAnimationAndFrame(stopAnimName, 0, new(0, -2));
			DisableAllSwords();
		}
	}

	private void InterruptAttack()
	{
		_attackSessionId++;
		_isAttacking = false;
		DisableAllSwords();
	}

	private void EnableSword(Affectables sword)
	{
		if (sword == null) return;
		sword.SetDeferred(Area2D.PropertyName.Monitoring, true);
		sword.SetDeferred(Area2D.PropertyName.Monitorable, true);
		sword.SetDeferred(Node.PropertyName.ProcessMode, Variant.From(ProcessModeEnum.Always));
	}

	private void DisableSword(Affectables sword)
	{
		if (sword == null) return;
		sword.SetDeferred(Area2D.PropertyName.Monitoring, false);
		sword.SetDeferred(Area2D.PropertyName.Monitorable, false);
		sword.SetDeferred(Node.PropertyName.ProcessMode, Variant.From(ProcessModeEnum.Disabled));
	}

	private void DisableAllSwords()
	{
		DisableSword(_swordLeft);
		DisableSword(_swordRight);
		DisableSword(_swordUp);
		DisableSword(_swordDown);
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is Affectables affectable)
		{
			switch (affectable.Type)
			{
				case Affectables.EffectType.DAMAGE:
					InterruptAttack();
					health.applyHealthEffect(-affectable.Value);
					GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, health.health, UIHEALTH);

					var direction = affectable.Direction ?? -_previousDirection;

					_movementComponent?.ApplyForce(direction, 0.35f);
					_animationComponent?.StartColorFluctuation(0.30f);

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
