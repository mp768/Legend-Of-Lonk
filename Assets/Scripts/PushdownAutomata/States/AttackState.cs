using Godot;

[GlobalClass]
public partial class AttackState : State
{
    [ExportGroup("Weapon Hitboxes")]
    [Export] private Affectables _swordLeft;
    [Export] private Affectables _swordRight;
    [Export] private Affectables _swordDown;
    [Export] private Affectables _swordUp;

    [ExportGroup("Attack Configuration")]
    [Export] public float AttackDuration = 0.5f;

    private float _timer = 0.0f;
    private int _currentFrameIndex = 1;
    private Vector2 _attackDirection = Vector2.Down;

	private Vector2I _prevSpriteOffset;

    public override void Initialize(PushdownStateMachine machine, CharacterBody2D entity, SpritePresenter presenter)
    {
        base.Initialize(machine, entity, presenter);

        // Configure directional vectors for hitboxes
        if (_swordLeft != null) _swordLeft.Direction = new Vector2(-1, 0);
        if (_swordRight != null) _swordRight.Direction = new Vector2(1, 0);
        if (_swordUp != null) _swordUp.Direction = new Vector2(0, -1);
        if (_swordDown != null) _swordDown.Direction = new Vector2(0, 1);

        DisableAllSwords();
    }

    public void SetupAttack(int frameIndex, Vector2 direction)
    {
        _currentFrameIndex = frameIndex;
        _attackDirection = direction == Vector2.Zero ? Vector2.Down : direction;
    }

    public override void Enter()
    {
        if (Entity != null) Entity.Velocity = Vector2.Zero;
        _timer = AttackDuration;

		_prevSpriteOffset = Visuals.SpriteOffset;

        DisableAllSwords();
        ExecuteDirectionalAttack();
    }

    public override void PhysicsUpdate(double delta)
    {
        if (Entity != null) Entity.Velocity = Vector2.Zero;

        _timer -= (float)delta;
        if (_timer <= 0.0f)
        {
            Machine.PopState(); // Pop attack state off stack when finished
            return;
        }

        Visuals?.UpdateSubpixelPosition();
    }

    public override void Exit()
    {
        DisableAllSwords();
        if (Entity != null) Entity.Velocity = Vector2.Zero;

		Visuals.SpriteOffset = _prevSpriteOffset;
    }

    private void ExecuteDirectionalAttack()
    {
        string animName = "sword_down";
        Vector2I spriteOffset = new(0, -2);
        bool flipH = false;
        Affectables activeSword = null;

        if (_attackDirection.X > 0)
        {
            animName = "sword_horizontal";
            flipH = false;
            if (_currentFrameIndex == 1)
            {
                spriteOffset = new(6, -2);
                activeSword = _swordRight;
            }
        }
        else if (_attackDirection.X < 0)
        {
            animName = "sword_horizontal";
            flipH = true;
            if (_currentFrameIndex == 1)
            {
                spriteOffset = new(-6, -2);
                activeSword = _swordLeft;
            }
        }
        else if (_attackDirection.Y < 0)
        {
            animName = "sword_up";
            spriteOffset = new(0, -10);
            if (_currentFrameIndex == 1) activeSword = _swordUp;
        }
        else if (_attackDirection.Y > 0)
        {
            animName = "sword_down";
            spriteOffset = new(0, 5);
            if (_currentFrameIndex == 1) activeSword = _swordDown;
        }

        if (activeSword != null)
        {
            EnableSword(activeSword);
        }

        // Display attack pose via shared SpritePresenter
        if (Visuals != null)
        {
            Visuals.SpriteOffset = spriteOffset;
            Visuals.SetSingleFramePose(animName, _currentFrameIndex, flipH);
            Visuals.UpdateSubpixelPosition();
        }
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
}