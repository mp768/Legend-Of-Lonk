using Godot;

[GlobalClass]
public partial class GridAnimationComponent : Node
{
    [Export] public AnimatedSprite2D sprite;
    [Export] public int stepFrequency = 6;
    [Export] public Vector2I spriteOffset = new(0, -2);

    [ExportGroup("Animation Names")]
    [Export] public string animHorizontal = "walk_horizontal";
    [Export] public string animDown = "walk_down";
    [Export] public string animUp = "walk_up";

    [Export] public float nesStepDuration = 0.05f; // Fast flicker (~3 frames at 60 FPS)


    private int _movementFrameCounter = 0;
    private Node2D _parent;

    // Classic NES Zelda-style color sequence (Red, Blue/Cyan, Orange/Yellow, Normal)
    private static readonly Color[] DefaultNesPalette = new Color[]
    {
        Color.FromHtml("#d84000"), // NES Red
        Color.FromHtml("#0078f8"), // NES Blue
        Color.FromHtml("#fc9838"), // NES Orange
        Colors.White               // Base sprite color
    };private Tween _fluctuationTween;
    private int _fluctuationSessionId = 0;

    public override void _Ready()
    {
        _parent = GetParent<Node2D>();

        if (sprite != null)
        {
            sprite.Stop(); // Take manual control of animation stepping
        }
    }

    public void UpdateAnimation(Vector2 moveDirection)
    {
        if (sprite == null) return;

        if (moveDirection != Vector2.Zero)
        {
            UpdateFacing(moveDirection);
            AdvanceAnimationFrame();
        }
        else
        {
            _movementFrameCounter = 0;
        }

        UpdateSubpixelPosition();
    }

    private void UpdateFacing(Vector2 direction)
    {
        if (direction.X > 0)
        {
            sprite.Animation = animHorizontal;
            sprite.FlipH = false;
        }
        else if (direction.X < 0)
        {
            sprite.Animation = animHorizontal;
            sprite.FlipH = true;
        }
        else if (direction.Y > 0)
        {
            sprite.Animation = animDown;
        }
        else if (direction.Y < 0)
        {
            sprite.Animation = animUp;
        }
    }

    private void AdvanceAnimationFrame()
    {
        _movementFrameCounter++;
        if (_movementFrameCounter >= stepFrequency)
        {
            _movementFrameCounter = 0;
            
            if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation(sprite.Animation))
            {
                int frameCount = sprite.SpriteFrames.GetFrameCount(sprite.Animation);
                if (frameCount > 0)
                {
                    sprite.Frame = (sprite.Frame + 1) % frameCount;
                }
            }
        }
    }

    private void UpdateSubpixelPosition()
    {
        if (_parent == null) return;

        // Keeps pixel art visuals snapped to integer pixels regardless of float physics position
        sprite.Position = new Vector2(
            Mathf.Round(_parent.Position.X) - _parent.Position.X + spriteOffset.X,
            Mathf.Round(_parent.Position.Y) - _parent.Position.Y + spriteOffset.Y
        );
    }

    public async void StartColorFluctuation(float durationSeconds)
    {
        if (sprite == null || durationSeconds <= 0) return;

        StopColorFluctuation(); // Clear existing effect

        int currentSession = ++_fluctuationSessionId;
        Color[] cyclePalette = DefaultNesPalette;
        float speed = nesStepDuration;

        if (cyclePalette.Length == 0) return;

        // Build a step-based sequence without smooth fading (instant color switches)
        _fluctuationTween = CreateTween().SetLoops();
        foreach (Color color in cyclePalette)
        {
            Color targetColor = color;
            _fluctuationTween.TweenCallback(Callable.From(() => 
            {
                if (IsInstanceValid(sprite))
                {
                    sprite.SelfModulate = targetColor;
                }
            }));
            _fluctuationTween.TweenInterval(speed);
        }

        // Run effect for designated duration
        await ToSignal(GetTree().CreateTimer(durationSeconds), SceneTreeTimer.SignalName.Timeout);

        // Reset if this specific timer session is still active
        if (IsInstanceValid(this) && currentSession == _fluctuationSessionId)
        {
            StopColorFluctuation();
        }
    }

    public void StopColorFluctuation()
    {
        _fluctuationSessionId++;

        if (_fluctuationTween != null && _fluctuationTween.IsValid())
        {
            _fluctuationTween.Kill();
            _fluctuationTween = null;
        }

        if (IsInstanceValid(sprite))
        {
            sprite.SelfModulate = Colors.White;
        }
    }

    public void SetAnimationAndFrame(string animationName, int frameIndex, Vector2I newSpriteOffset, bool? flipH = null)
    {
        if (sprite == null) return;

        if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation(animationName))
        {
            sprite.Animation = animationName;

            if (flipH.HasValue)
            {
                sprite.FlipH = flipH.Value;
            }

            int frameCount = sprite.SpriteFrames.GetFrameCount(animationName);
            if (frameCount > 0)
            {
                sprite.Frame = Mathf.Clamp(frameIndex, 0, frameCount - 1);
            }
        }

        var originalSpriteOffset = spriteOffset;
        spriteOffset = newSpriteOffset;
        UpdateSubpixelPosition();
        spriteOffset = originalSpriteOffset;

        _movementFrameCounter = 0; // Reset movement frame counter to sync step timing
    }
}