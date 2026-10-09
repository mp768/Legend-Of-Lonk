using Godot;

[GlobalClass]
public partial class PlayerInputProvider : Node, IInputProvider
{
    public ICommand FetchNextCommand()
    {
        if (Input.IsActionJustPressed("standard_attack"))
        {
            return new AttackCommand(AttackState.AttackKind.SWORD);
        }

        if (Input.IsActionJustPressed("alternate_attack"))
        {
            return new AttackCommand(AttackState.AttackKind.ITEM);
        }

        if (Input.IsActionJustPressed("cycle_weapon"))
        {
            return new CycleWeaponCommand();
        }

        // Both axes go through as pressed, and the movement state decides how to handle diagonals.
        Vector2 input = new(AxisSign("left", "right"), AxisSign("up", "down"));
        return new MoveCommand(input);
    }

    private static int AxisSign(string negativeAction, string positiveAction)
    {
        float axis = Input.GetAxis(negativeAction, positiveAction);
        return Mathf.IsZeroApprox(axis) ? 0 : Mathf.Sign(axis);
    }
}
