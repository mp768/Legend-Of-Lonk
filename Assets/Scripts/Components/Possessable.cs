using Godot;

// Lets an enemy be possessed. While possessed, the enemy sleeps in its room and a player-side copy of it,
// the vessel, takes over as the player. When the vessel is released or dies, the enemy wakes up as good as new.
[GlobalClass]
public partial class Possessable : Node
{
    // Must have a PossessedEnemy root.
    [Export] public PackedScene VesselScene { get; set; }

    public PossessedEnemy Vessel { get; private set; }

    private Entity host;
    private PlayerEntity possessor;

    // Called from the host's hitbox handler. Returns false if the possession didn't take.
    public bool TryPossess(Entity target, PlayerEntity by)
    {
        if (IsInstanceValid(Vessel) || VesselScene == null || !by.IsPlayer)
        {
            return false;
        }

        if (VesselScene.Instantiate() is not PossessedEnemy vessel)
        {
            GD.PushError($"{VesselScene.ResourcePath} must have a {nameof(PossessedEnemy)} root.");
            return false;
        }

        if (!new PossessCommand(by).Execute(target, target.StateMachine))
        {
            // Never entered the tree.
            vessel.Free();
            return false;
        }

        host = target;
        possessor = by;
        Vessel = vessel;

        // Beside the possessor, outside every room, so it keeps processing when it walks into another one.
        Node parent = by.GetParent();
        vessel.Position = parent is Node2D parent2D ? parent2D.ToLocal(target.GlobalPosition) : target.GlobalPosition;
        vessel.FacingDirection = target.FacingDirection;
        vessel.Died += OnVesselDied;

        by.DropPlayerRole();

        // Deferred because possession lands inside a hitbox callback. The role is taken once the vessel is in
        // the tree, so its Health is ready to report.
        parent.CallDeferred(Node.MethodName.AddChild, vessel);
        vessel.CallDeferred(PlayerEntity.MethodName.TakePlayerRole);

        GameSignals.Instance.EmitSignal(GameSignals.SignalName.PossessionStarted);
        return true;
    }

    private void OnVesselDied()
    {
        Vessel.DropPlayerRole();
        Vessel = null;

        // The host never moved while dormant, so it comes back where it was possessed.
        if (IsInstanceValid(host))
        {
            host.Health?.Refill();
            new WakeCommand().Execute(host, host.StateMachine);
        }

        if (IsInstanceValid(possessor))
        {
            new WakeCommand().Execute(possessor, possessor.StateMachine);
            possessor.TakePlayerRole();
        }

        GameSignals.Instance.EmitSignal(GameSignals.SignalName.PossessionEnded);
    }
}
