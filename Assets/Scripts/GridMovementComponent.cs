using Godot;

[GlobalClass]
public partial class GridMovementComponent : Node
{
    [Export] public float MoveSpeed = 60.0f;
    private const float GRID_SIZE = 8.0f;

    private CharacterBody2D _body;

    // Forced movement state variables
    private float _forceTimer = 0.0f;
    private Vector2 _forcedDirection = Vector2.Zero;
    private float _forcedSpeed = -1.0f;

    // Returns true if a forced movement effect is currently active.
    public bool IsForcedMoving => _forceTimer > 0.0f;

    public override void _Ready()
    {
        _body = GetParent<CharacterBody2D>();
        if (_body == null)
        {
            GD.PrintErr("GridMovementComponent must be a child of a CharacterBody2D");
        }
    }

    // Applies the calculated velocity and moves the CharacterBody2D. 
    // Hijacks controls if forced movement is active.
    public void Move(Vector2 direction, double delta)
    {
        if (_body == null) return;

        Vector2 effectiveDirection = direction;
        float effectiveSpeed = MoveSpeed;

        // Hijack input if force duration is active
        if (IsForcedMoving)
        {
            _forceTimer -= (float)delta;
            effectiveDirection = _forcedDirection;
            effectiveSpeed = _forcedSpeed;
        }

        if (effectiveDirection != Vector2.Zero)
        {
            // Temporarily swap MoveSpeed to allow custom force speeds in CalculateGridAlignedVelocity
            float originalSpeed = MoveSpeed;
            MoveSpeed = effectiveSpeed;

            _body.Velocity = CalculateGridAlignedVelocity(effectiveDirection, (float)delta, snapPosition: true);

            MoveSpeed = originalSpeed;
        }
        else
        {
            _body.Velocity = Vector2.Zero;
        }

        _body.MoveAndSlide();
    }

    /// <summary>
    /// Steers and snaps the perpendicular axis onto the nearest grid box.
    /// Exposed publicly so parent scripts can use it for `TestMove` collision prediction.
    /// </summary>
    public Vector2 CalculateGridAlignedVelocity(Vector2 inputDir, float delta, bool snapPosition = true)
    {
        Vector2 velocity = inputDir * MoveSpeed;

        // Moving VERTICALLY: Pull X axis toward nearest grid box
        if (inputDir.Y != 0.0f)
        {
            float targetX = Mathf.Round(_body.Position.X / GRID_SIZE) * GRID_SIZE;
            float diffX = targetX - _body.Position.X;

            if (Mathf.Abs(diffX) > 0.01f)
            {
                if (Mathf.Abs(diffX) <= MoveSpeed * delta)
                {
                    if (snapPosition)
                    {
                        _body.Position = new Vector2(targetX, _body.Position.Y);
                        velocity.X = 0.0f;
                    }
                    else
                    {
                        velocity.X = diffX / delta;
                    }
                }
                else
                {
                    velocity.X = Mathf.Sign(diffX) * MoveSpeed;
                }
            }
        }
        // Moving HORIZONTALLY: Pull Y axis toward nearest grid box
        else if (inputDir.X != 0.0f)
        {
            float targetY = Mathf.Round(_body.Position.Y / GRID_SIZE) * GRID_SIZE;
            float diffY = targetY - _body.Position.Y;

            if (Mathf.Abs(diffY) > 0.01f)
            {
                if (Mathf.Abs(diffY) <= MoveSpeed * delta)
                {
                    if (snapPosition)
                    {
                        _body.Position = new Vector2(_body.Position.X, targetY);
                        velocity.Y = 0.0f;
                    }
                    else
                    {
                        velocity.Y = diffY / delta;
                    }
                }
                else
                {
                    velocity.Y = Mathf.Sign(diffY) * MoveSpeed;
                }
            }
        }

        return velocity;
    }

    public void ApplyForce(Vector2 direction, float duration, float speed = -1.0f)
    {
        if (direction == Vector2.Zero || duration <= 0.0f) return;

        _forcedDirection = direction.Normalized();
        _forceTimer = duration;
        _forcedSpeed = MoveSpeed;
    }
}