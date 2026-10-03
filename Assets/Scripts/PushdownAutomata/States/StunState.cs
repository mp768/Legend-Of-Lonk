using Godot;

[GlobalClass]
public partial class StunState : State
{
    [ExportGroup("Physics Defaults")]
    [Export] public float DefaultDuration = 0.5f;
    [Export] public float DefaultKnockbackSpeed = 120.0f;
    [Export] public float DefaultDelayOffset = 0.0f;

    [ExportGroup("Visual Config")]
    [Export] public string StunAnimationName = "hurt";
    [Export] public int StunFrameIndex = 0;
    [Export] public bool EnablePaletteFluctuation = true;
    [Export] public float FluctuationStepDuration = 0.05f;
    [Export] public Color[] FluctuationPalette;

    private float _stunTimer = 0.0f;
    private float _delayOffsetTimer = 0.0f;
    private Vector2 _knockbackDirection = Vector2.Zero;
    private float _knockbackSpeed = 0.0f;

    public StunState()
    {
        IsInterruptable = false;
    }

    public void ApplyStun(float duration, Vector2 knockbackDir = default, float knockbackSpeed = -1f, float delayOffset = -1f)
    {
        _stunTimer = duration > 0f ? duration : DefaultDuration;
        _knockbackDirection = knockbackDir.Normalized();
        _knockbackSpeed = knockbackSpeed >= 0f ? knockbackSpeed : DefaultKnockbackSpeed;
        _delayOffsetTimer = delayOffset >= 0f ? delayOffset : DefaultDelayOffset;

        Machine.PushState(this);
    }

    public override void Enter()
    {
        if (Entity != null) Entity.Velocity = Vector2.Zero;

        // Apply single-frame pose and palette flicker through the visual presenter
        Visuals?.SetSingleFramePose(StunAnimationName, StunFrameIndex);

        if (EnablePaletteFluctuation)
        {
            Visuals?.StartColorFluctuation(FluctuationPalette, FluctuationStepDuration);
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        float fDelta = (float)delta;
        _stunTimer -= fDelta;

        if (_stunTimer <= 0.0f)
        {
            Machine.PopState();
            return;
        }

        if (_delayOffsetTimer > 0.0f)
        {
            _delayOffsetTimer -= fDelta;
            Entity.Velocity = Vector2.Zero;
        }
        else if (_knockbackDirection != Vector2.Zero)
        {
            Entity.Velocity = _knockbackDirection * _knockbackSpeed;
        }

        Entity.MoveAndSlide();
        Visuals?.UpdateSubpixelPosition();
    }

    public override void Exit()
    {
        Visuals?.StopColorFluctuation();
        if (Entity != null) Entity.Velocity = Vector2.Zero;
    }
}