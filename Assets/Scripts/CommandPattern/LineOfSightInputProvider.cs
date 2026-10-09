using Godot;

// Watches along its row and column for the player character's hurtbox and lunges at it when spotted.
// Walls block its view. Used by blade traps.
[GlobalClass]
public partial class LineOfSightInputProvider : Node, IInputProvider
{
    private static readonly Vector2[] CARDINALS = [Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down];

    [Export] private Node2D eyes;

    [Export] public float SightRange { get; set; } = 256f;

    // Physics ticks between checks.
    [Export] public int CheckInterval { get; set; } = 4;

    private int ticksUntilCheck;

    public ICommand FetchNextCommand()
    {
        if (--ticksUntilCheck > 0)
        {
            return null;
        }
        ticksUntilCheck = CheckInterval;

        PhysicsDirectSpaceState2D space = eyes.GetWorld2D().DirectSpaceState;
        foreach (Vector2 direction in CARDINALS)
        {
            var query = PhysicsRayQueryParameters2D.Create(
                eyes.GlobalPosition,
                eyes.GlobalPosition + direction * SightRange,
                Constants.PLAYER_HURTBOX_LAYER | Constants.WALLS_LAYER);
            query.CollideWithAreas = true;

            var hit = space.IntersectRay(query);
            if (hit.Count > 0 && hit["collider"].As<GodotObject>() is Area2D)
            {
                return new LungeCommand(direction);
            }
        }

        return null;
    }
}
