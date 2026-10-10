using Godot;
using System.Collections.Generic;

// Sends Wallmasters out of the wall nearest the player character on a cooldown while its room is active,
// and removes them when they finish their path or the room is left. Place it at the room's origin.
[GlobalClass]
public partial class WallmasterSpawner : Node2D, IResettableEntity
{
    // Must have an Enemy root with a WallmasterInputProvider child.
    [Export] public PackedScene WallmasterScene { get; set; }

    [Export] public float Cooldown { get; set; } = 4f;
    [Export] public int MaxAlive { get; set; } = 1;

    // How far along the wall a Wallmaster travels, centred on the player's position.
    [Export] public float AlongDistance { get; set; } = 64f;

    private readonly List<Node2D> alive = [];
    private Timer cooldownTimer;

    public override void _Ready()
    {
        cooldownTimer = new Timer { OneShot = false, WaitTime = Cooldown };
        cooldownTimer.Timeout += TrySpawn;
        AddChild(cooldownTimer);
    }

    public void OnRoomEntered()
    {
        cooldownTimer.Start();
    }

    public void OnRoomExited()
    {
        cooldownTimer.Stop();
        foreach (Node2D wallmaster in alive)
        {
            if (IsInstanceValid(wallmaster))
            {
                wallmaster.QueueFree();
            }
        }
        alive.Clear();
    }

    public override void _ExitTree()
    {
        cooldownTimer?.Stop();
    }

    private void TrySpawn()
    {
        alive.RemoveAll(w => !IsInstanceValid(w) || w.IsQueuedForDeletion());
        if (alive.Count >= MaxAlive || WallmasterScene == null)
        {
            return;
        }

        if (GetTree().GetFirstNodeInGroup(Constants.PLAYER_GROUP) is not Node2D player)
        {
            return;
        }

        if (WallmasterScene.Instantiate() is not Enemy wallmaster)
        {
            return;
        }

        var path = FindPathProvider(wallmaster);
        if (path == null)
        {
            GD.PushError($"{WallmasterScene.ResourcePath} has no {nameof(WallmasterInputProvider)}.");
            wallmaster.Free();
            return;
        }

        Vector2 target = ToLocal(player.GlobalPosition);
        Rect2 floor = Constants.ROOM_INTERIOR;

        // Come out of whichever wall is closest to the player.
        float toLeft = target.X - floor.Position.X;
        float toRight = floor.End.X - target.X;
        float toTop = target.Y - floor.Position.Y;
        float toBottom = floor.End.Y - target.Y;
        float nearest = Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toTop, toBottom));

        Vector2 outward;
        Vector2 along;
        Vector2 start;
        float halfTile = Constants.TILE_SIZE / 2f;
        bool flip = GD.Randf() < 0.5f;

        if (nearest == toLeft || nearest == toRight)
        {
            outward = nearest == toLeft ? Vector2.Right : Vector2.Left;
            along = flip ? Vector2.Up : Vector2.Down;
            float wallX = nearest == toLeft ? floor.Position.X - halfTile : floor.End.X + halfTile;
            start = new Vector2(wallX, target.Y - along.Y * AlongDistance / 2f);
        }
        else
        {
            outward = nearest == toTop ? Vector2.Down : Vector2.Up;
            along = flip ? Vector2.Left : Vector2.Right;
            float wallY = nearest == toTop ? floor.Position.Y - halfTile : floor.End.Y + halfTile;
            start = new Vector2(target.X - along.X * AlongDistance / 2f, wallY);
        }

        float outDistance = Mathf.Abs((target - start).Dot(outward)) + halfTile;
        path.Configure(outward, outDistance, along, AlongDistance);

        wallmaster.Position = start;
        path.PathFinished += () =>
        {
            if (IsInstanceValid(wallmaster))
            {
                wallmaster.QueueFree();
            }
        };

        // Every wallmaster that gets defeated should lower the amount allowed to spawn from there on.
        wallmaster.Defeated += () => MaxAlive--;

        alive.Add(wallmaster);
        AddChild(wallmaster);
    }

    private static WallmasterInputProvider FindPathProvider(Node wallmaster)
    {
        foreach (Node child in wallmaster.GetChildren())
        {
            if (child is WallmasterInputProvider provider)
            {
                return provider;
            }
        }
        return null;
    }
}
