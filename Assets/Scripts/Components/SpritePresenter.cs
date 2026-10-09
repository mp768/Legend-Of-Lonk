using Godot;

// Drives an entity's AnimatedSprite2D by hand, NES-style. Walk frames advance with movement rather than
// time, the sprite is drawn on whole pixels, and hits can flash it through a colour palette.
// States tell the presenter what to show; the presenter decides how.
[GlobalClass]
public partial class SpritePresenter : Node
{
    private static readonly Color[] NES_FLASH_PALETTE =
    [
        new("#d84000"), // red
        new("#0078f8"), // blue
        new("#fc9838"), // orange
        Colors.White,
    ];

    [Export] public AnimatedSprite2D Sprite { get; set; }

    // Where the sprite sits relative to the entity's origin when no pose overrides it.
    [Export] public Vector2I SpriteOffset { get; set; } = new(0, -2);

    // Allows a SpritePresenter to run on its own.
    [Export] public bool AutoAdvanceAnimation { get; set; } = false;

    [ExportGroup("Walk Animation")]
    [Export] public string WalkHorizontal { get; set; } = "walk_horizontal";
    [Export] public string WalkDown { get; set; } = "walk_down";
    [Export] public string WalkUp { get; set; } = "walk_up";

    // Physics ticks spent on each walk frame.
    [Export] public int WalkStepFrequency { get; set; } = 6;

    [ExportGroup("Flash")]
    [Export] public float FlashStepDuration { get; set; } = 0.05f;

    // Leave empty to use the NES palette.
    [Export] public Color[] FlashPalette { get; set; } = [];

    private int walkTickCounter;
    private Vector2I? poseOffset;
    private Tween flashTween;

    public override void _Ready()
    {
        // Frames are stepped manually, never by the sprite's own playback.
        Sprite?.Stop();
    }

    public override void _Process(double delta)
    {
        if (AutoAdvanceAnimation) StepWalk(Vector2.Up);
        SnapToPixelGrid();
    }

    public override void _ExitTree()
    {
        KillFlashTween();
    }

    // Advances the walk cycle one tick in the given direction. A zero direction holds the current frame mid-stride.
    public void StepWalk(Vector2 direction)
    {
        if (Sprite == null)
        {
            return;
        }

        if (direction == Vector2.Zero)
        {
            walkTickCounter = 0;
            return;
        }

        FaceWalk(direction);

        walkTickCounter++;
        if (walkTickCounter >= WalkStepFrequency)
        {
            walkTickCounter = 0;
            AdvanceFrame();
        }
    }

    // Shows the walk animation for a direction without advancing it.
    public void FaceWalk(Vector2 direction)
    {
        if (Sprite == null)
        {
            return;
        }

        if (direction.X != 0f)
        {
            Sprite.Animation = WalkHorizontal;
            Sprite.FlipH = direction.X < 0f;
        }
        else if (direction.Y > 0f)
        {
            Sprite.Animation = WalkDown;
        }
        else if (direction.Y < 0f)
        {
            Sprite.Animation = WalkUp;
        }
    }

    // Holds a single frame, e.g. an attack or hurt pose. An offset replaces SpriteOffset until the next
    // pose or ClearPoseOffset.
    public void ShowPose(string animation, int frame, bool? flipH = null, Vector2I? offset = null)
    {
        if (Sprite?.SpriteFrames == null || !Sprite.SpriteFrames.HasAnimation(animation))
        {
            return;
        }

        Sprite.Animation = animation;
        if (flipH.HasValue)
        {
            Sprite.FlipH = flipH.Value;
        }

        int frameCount = Sprite.SpriteFrames.GetFrameCount(animation);
        if (frameCount > 0)
        {
            Sprite.Frame = Mathf.Clamp(frame, 0, frameCount - 1);
        }

        poseOffset = offset;
        walkTickCounter = 0;
        SnapToPixelGrid();
    }

    public void ClearPoseOffset()
    {
        poseOffset = null;
        SnapToPixelGrid();
    }

    // Cycles the sprite through the flash palette for `duration` seconds, then restores its colour.
    public void Flash(float duration)
    {
        if (Sprite == null || duration <= 0f)
        {
            return;
        }

        StopFlash();

        Color[] palette = FlashPalette.Length > 0 ? FlashPalette : NES_FLASH_PALETTE;
        float step = Mathf.Max(FlashStepDuration, 0.01f);
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(duration / step));

        flashTween = CreateTween();
        for (int i = 0; i < stepCount; i++)
        {
            Color color = palette[i % palette.Length];
            flashTween.TweenCallback(Callable.From(() => Sprite.SelfModulate = color));
            flashTween.TweenInterval(step);
        }
        flashTween.TweenCallback(Callable.From(ResetColor));
    }

    public void StopFlash()
    {
        KillFlashTween();
        ResetColor();
    }

    private void KillFlashTween()
    {
        if (flashTween != null && flashTween.IsValid())
        {
            flashTween.Kill();
        }
        flashTween = null;
    }

    private void ResetColor()
    {
        if (IsInstanceValid(Sprite))
        {
            Sprite.SelfModulate = Colors.White;
        }
    }

    private void AdvanceFrame()
    {
        if (Sprite.SpriteFrames == null || !Sprite.SpriteFrames.HasAnimation(Sprite.Animation))
        {
            return;
        }

        int frameCount = Sprite.SpriteFrames.GetFrameCount(Sprite.Animation);
        if (frameCount > 0)
        {
            Sprite.Frame = (Sprite.Frame + 1) % frameCount;
        }
    }

    // Physics positions are fractional, but pixel art has to land on whole pixels to stay crisp.
    private void SnapToPixelGrid()
    {
        if (Sprite == null)
        {
            return;
        }

        Sprite.Position = poseOffset ?? SpriteOffset;
        Sprite.GlobalPosition = Sprite.GlobalPosition.Round();
    }
}
