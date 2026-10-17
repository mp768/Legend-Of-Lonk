using Godot;

// Keeps its children hidden and switched off until Fire() is called, then reveals them in place. Used for
// rewards that appear when a room is cleared or a block is pushed.
[GlobalClass]
public partial class HiddenItem : Node2D
{
    public bool IsRevealed { get; private set; }

    public override void _Ready()
    {
        Visible = false;

        // A disabled area is taken out of the physics space, so the item can't be collected early.
        ProcessMode = ProcessModeEnum.Disabled;
    }

    public void Fire()
    {
        if (IsRevealed)
        {
            return;
        }

        IsRevealed = true;
        Visible = true;

        // Deferred because it usually fires from an enemy's death inside a physics callback.
        SetDeferred(Node.PropertyName.ProcessMode, Variant.From(ProcessModeEnum.Inherit));
    }
}
