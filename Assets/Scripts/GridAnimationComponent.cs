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

    private int _movementFrameCounter = 0;
    private Node2D _parent;

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
}