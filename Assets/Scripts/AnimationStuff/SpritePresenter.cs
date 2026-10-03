using Godot;

[GlobalClass]
public partial class SpritePresenter : Node
{
    [Export] public AnimatedSprite2D Sprite;
    [Export] public Vector2I SpriteOffset = new(0, -2);
    [Export] public float DefaultNesStepDuration = 0.05f;

    private int _movementFrameCounter = 0;
    private Node2D _parentEntity;
    private Tween _fluctuationTween;

    private static readonly Color[] DefaultNesPalette = new Color[]
    {
        new Color("#d84000"), // NES Red
        new Color("#0078f8"), // NES Blue
        new Color("#fc9838"), // NES Orange
        Colors.White
    };

    public override void _Ready()
    {
        _parentEntity = GetParent<Node2D>();
        if (Sprite != null)
        {
            Sprite.Stop(); // Retain manual frame stepping control
        }
    }

    /// <summary>
    /// Advances directional walking animations based on step frequency, or pauses mid-stride on Vector2.Zero.
    /// </summary>
    public void StepDirectionalAnimation(Vector2 direction, string animHorizontal, string animDown, string animUp, int stepFrequency)
    {
        if (Sprite == null) return;

        if (direction != Vector2.Zero)
        {
            UpdateFacing(direction, animHorizontal, animDown, animUp);
            AdvanceAnimationFrame(stepFrequency);
        }
        else
        {
            _movementFrameCounter = 0; // Freeze frame on current stance
        }
    }

    /// <summary>
    /// Forces the sprite to a specific animation and single frame (e.g. hurt, attack, casting).
    /// </summary>
    public void SetSingleFramePose(string animationName, int frameIndex = 0, bool? flipH = null)
    {
        if (Sprite == null || Sprite.SpriteFrames == null) return;
        if (!Sprite.SpriteFrames.HasAnimation(animationName)) return;

        Sprite.Animation = animationName;
        if (flipH.HasValue) Sprite.FlipH = flipH.Value;

        int frameCount = Sprite.SpriteFrames.GetFrameCount(animationName);
        if (frameCount > 0)
        {
            Sprite.Frame = Mathf.Clamp(frameIndex, 0, frameCount - 1);
        }
        _movementFrameCounter = 0;
    }

    /// <summary>
    /// Snaps rendering visuals to integer screen coordinates while keeping float-based collision vectors intact.
    /// </summary>
    public void UpdateSubpixelPosition()
    {
        if (Sprite == null || _parentEntity == null) return;

        Sprite.Position = new Vector2(
            Mathf.Round(_parentEntity.Position.X) - _parentEntity.Position.X + SpriteOffset.X,
            Mathf.Round(_parentEntity.Position.Y) - _parentEntity.Position.Y + SpriteOffset.Y
        );
    }

    /// <summary>
    /// Runs a looped palette swap effect across a sequence of colors.
    /// </summary>
    public void StartColorFluctuation(Color[] palette = null, float stepDuration = -1f)
    {
        if (Sprite == null) return;

        StopColorFluctuation();

        Color[] cyclePalette = (palette != null && palette.Length > 0) ? palette : DefaultNesPalette;
        float speed = stepDuration > 0f ? stepDuration : DefaultNesStepDuration;

        _fluctuationTween = CreateTween().SetLoops();
        foreach (Color color in cyclePalette)
        {
            Color targetColor = color;
            _fluctuationTween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(Sprite))
                {
                    Sprite.SelfModulate = targetColor;
                }
            }));
            _fluctuationTween.TweenInterval(speed);
        }
    }

    public void StopColorFluctuation()
    {
        if (_fluctuationTween != null && _fluctuationTween.IsValid())
        {
            _fluctuationTween.Kill();
            _fluctuationTween = null;
        }

        if (IsInstanceValid(Sprite))
        {
            Sprite.SelfModulate = Colors.White;
        }
    }

    private void UpdateFacing(Vector2 direction, string animHorizontal, string animDown, string animUp)
    {
        if (direction.X > 0)
        {
            Sprite.Animation = animHorizontal;
            Sprite.FlipH = false;
        }
        else if (direction.X < 0)
        {
            Sprite.Animation = animHorizontal;
            Sprite.FlipH = true;
        }
        else if (direction.Y > 0)
        {
            Sprite.Animation = animDown;
        }
        else if (direction.Y < 0)
        {
            Sprite.Animation = animUp;
        }
    }

    private void AdvanceAnimationFrame(int stepFrequency)
    {
        if (Sprite == null || Sprite.SpriteFrames == null) return;

        _movementFrameCounter++;
        if (_movementFrameCounter >= stepFrequency)
        {
            _movementFrameCounter = 0;
            if (Sprite.SpriteFrames.HasAnimation(Sprite.Animation))
            {
                int frameCount = Sprite.SpriteFrames.GetFrameCount(Sprite.Animation);
                if (frameCount > 0)
                {
                    Sprite.Frame = (Sprite.Frame + 1) % frameCount;
                }
            }
        }
    }
}