using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class PossessedEnemy : PlayerEntity
{
    private static readonly Vector2[] OUTLINE_DIRECTIONS = [Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down];

    [Signal]
    public delegate void DiedEventHandler();

    // Helps to set it apart from the identical enemies around it. 
    [Export] public Color OutlineColor { get; set; } = Colors.White;

    // Fills each outline copy with one flat color
    [Export] private Shader outlineShader;

    private readonly List<AnimatedSprite2D> outlineCopies = [];
    private bool removed;

    public override void _Ready()
    {
        base._Ready();

        ignoredEffects.Add(Affectables.EffectType.MAX_HEALTH);

        CreateOutline();
    }

    public override void _Process(double delta)
    {
        SyncOutline();
    }

    public void Release() => Die();

    protected override void Die()
    {
        if (removed)
        {
            return;
        }
        removed = true;

        // Death is reported from a hitbox callback. Stop simulating now and free at the end of the frame.
        StateMachine?.SetPhysicsProcess(false);
        EmitSignal(SignalName.Died);
        QueueFree();
    }

    // Silhouette copies of the sprite, one pixel out in each direction and drawn behind it.
    private void CreateOutline()
    {
        AnimatedSprite2D sprite = Visuals?.Sprite;
        if (sprite == null || outlineShader == null)
        {
            return;
        }

        var material = new ShaderMaterial { Shader = outlineShader };
        material.SetShaderParameter("color", OutlineColor);

        foreach (Vector2 direction in OUTLINE_DIRECTIONS)
        {
            // Children of the sprite, so they follow its position, visibility and pixel snapping for free.
            var copy = new AnimatedSprite2D
            {
                SpriteFrames = sprite.SpriteFrames,
                Material = material,
                Position = direction,
                ShowBehindParent = true,
            };
            outlineCopies.Add(copy);
            sprite.AddChild(copy);
        }

        SyncOutline();
    }

    private void SyncOutline()
    {
        AnimatedSprite2D sprite = Visuals?.Sprite;
        if (sprite == null)
        {
            return;
        }

        foreach (AnimatedSprite2D copy in outlineCopies)
        {
            copy.Animation = sprite.Animation;
            copy.Frame = sprite.Frame;
            copy.FlipH = sprite.FlipH;
            copy.FlipV = sprite.FlipV;
            copy.Offset = sprite.Offset;
            copy.Centered = sprite.Centered;
        }
    }
}
