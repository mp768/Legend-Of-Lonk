using Godot;

// Out of action while something else acts on the entity's behalf: a possessor whose control has moved into
// another body, or an enemy whose possessed copy is walking around. It ignores input, can't be interrupted,
// and can't be touched. Only a WakeCommand ends it.
[GlobalClass]
public partial class DormantState : State
{
    public enum Look
    {
        HIDDEN,
        DIMMED,
    }

    [Export] public Look Appearance { get; set; } = Look.HIDDEN;

    // The pose held while DIMMED.
    [Export] public string PoseAnimation { get; set; } = "walk_down";

    public override bool IsInterruptable => false;
    public override bool IgnoresInput => true;

    public override void Enter()
    {
        Entity.Velocity = Vector2.Zero;
        Entity.MoveIntent = Vector2.Zero;
        Entity.SetTangible(false);
        Visuals?.StopFlash();

        if (Appearance == Look.HIDDEN)
        {
            Visuals?.SetHidden(true);
        }
        else
        {
            Visuals?.ShowPose(PoseAnimation, 0);
            Visuals?.SetDimmed(true);
        }
    }

    public override void Exit()
    {
        Entity.SetTangible(true);
        Visuals?.SetHidden(false);
        Visuals?.SetDimmed(false);
    }
}
