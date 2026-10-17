using Godot;
using System.Collections.Generic;

// One screen of the dungeon. A room knows its neighbours, reports when the player walks into one of its
// exits, only processes while it is the active room, and tracks whether its enemies have been defeated.
[GlobalClass]
public partial class Room : Node2D
{
	public enum RoomView
	{
		TOP_DOWN,
		SIDE_SCROLL,
	}

	// The four walls of a room, where its exits and doors are.
	public enum Side
	{
		UP,
		DOWN,
		LEFT,
		RIGHT,
	}

	// Emitted once, when the last enemy that counts toward clearing the room is defeated.
	[Signal]
	public delegate void ClearedEventHandler();

	// Emitted when the room's secret is found, e.g. a block is pushed. Lets the dungeon scene reveal things it
	// added to the room, like stairs, without pointing at nodes inside the room scene.
	[Signal]
	public delegate void SecretRevealedEventHandler();

	[Export] public Room leftRoom;
	[Export] public Room rightRoom;
	[Export] public Room upRoom;
	[Export] public Room downRoom;

	[Export] public RoomView View { get; set; } = RoomView.TOP_DOWN;

	[ExportGroup("Clearing")]
	// Closes the room's shutter doors while enemies remain.
	[Export] public bool LockUntilCleared { get; set; }

	public Vector2 Center => GlobalPosition + Constants.SCREEN_SIZE / 2;

	public bool IsCleared { get; private set; }

	private readonly List<Enemy> trackedEnemies = [];

	public override void _Ready()
	{
		Visible = false;
		SetProcessing(false);

		ConnectExit("LeftExit", leftRoom, Vector2.Left);
		ConnectExit("RightExit", rightRoom, Vector2.Right);
		ConnectExit("UpExit", upRoom, Vector2.Up);
		ConnectExit("DownExit", downRoom, Vector2.Down);

		foreach (Node child in GetChildren())
		{
			if (child is LockedDoor door)
			{
				door.Opened += () => OnLockedDoorOpened(door);
			}
		}
	}

	public Room NeighbourOn(Side side) => side switch
	{
		Side.UP => upRoom,
		Side.DOWN => downRoom,
		Side.LEFT => leftRoom,
		_ => rightRoom,
	};

	public static Side Opposite(Side side) => side switch
	{
		Side.UP => Side.DOWN,
		Side.DOWN => Side.UP,
		Side.LEFT => Side.RIGHT,
		_ => Side.LEFT,
	};

	// Opened from the other side of the doorway, so it stays silent: this room isn't the one on screen.
	public void OpenLockedDoor(Side side)
	{
		foreach (Node child in GetChildren())
		{
			if (child is LockedDoor door && door.Side == side && !door.IsOpen)
			{
				door.Open(playSound: false);
			}
		}
	}

	public void RevealSecret()
	{
		EmitSignal(SignalName.SecretRevealed);
	}

	public void Activate()
	{
		Visible = true;
		SetProcessing(true);

		foreach (Node child in GetChildren())
		{
			if (child is IResettableEntity entity)
			{
				entity.OnRoomEntered();
			}
		}

		if (!IsCleared)
		{
			TrackEnemies();
		}
	}

	// Freezes the room but leaves it visible, so it can scroll off screen during a transition.
	public void Deactivate()
	{
		SetProcessing(false);
		UntrackEnemies();

		foreach (Node child in GetChildren())
		{
			if (child is IResettableEntity entity)
			{
				entity.OnRoomExited();
			}
		}
	}

	private void TrackEnemies()
	{
		UntrackEnemies();

		foreach (Node child in GetChildren())
		{
			if (child is Enemy enemy && enemy.IsInGroup(Constants.ENEMY_GROUP) && enemy.CountsTowardClear && !enemy.IsQueuedForDeletion())
			{
				enemy.Defeated += OnEnemyDefeated;
				trackedEnemies.Add(enemy);
			}
		}

		// Covers rooms with no enemies and rooms emptied on an earlier visit.
		if (trackedEnemies.Count == 0)
		{
			MarkCleared();
		}
		else if (LockUntilCleared)
		{
			SetShuttersOpen(false);
		}
	}

	private void UntrackEnemies()
	{
		foreach (Enemy enemy in trackedEnemies)
		{
			if (IsInstanceValid(enemy))
			{
				enemy.Defeated -= OnEnemyDefeated;
			}
		}
		trackedEnemies.Clear();
	}

	private void OnEnemyDefeated()
	{
		trackedEnemies.RemoveAll(enemy => !IsInstanceValid(enemy) || enemy.IsQueuedForDeletion());
		if (trackedEnemies.Count == 0)
		{
			MarkCleared();
		}
	}

	private void MarkCleared()
	{
		if (IsCleared)
		{
			return;
		}

		IsCleared = true;

		if (LockUntilCleared)
		{
			SetShuttersOpen(true);
		}

        EmitSignal(SignalName.Cleared);
    }

    // Only touches shutters driven by clearing the room. Others, like one opened by a pushed block, are left alone.
    private void SetShuttersOpen(bool open)
    {
        foreach (Node child in GetChildren())
        {
            if (child is ShutterDoor shutter && shutter.OpensWhenCleared)
            {
                if (open)
                {
                    shutter.Open();
                }
                else
                {
                    shutter.Close();
                }
            }
        }
    }

    private void OnLockedDoorOpened(LockedDoor door)
    {
        // A room that lists itself as a neighbour is blocking that exit, like the entrance.
        Room neighbour = NeighbourOn(door.Side);
        if (neighbour != null && neighbour != this)
        {
            neighbour.OpenLockedDoor(Opposite(door.Side));
        }
    }

    private void SetProcessing(bool enabled)
    {
        // Deferred because rooms switch from inside exit callbacks, while physics is flushing queries.
        // A disabled room pauses physics, scripts and animations for all of its children.
        SetDeferred(Node.PropertyName.ProcessMode, Variant.From(enabled ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled));
    }

    // Rooms entered by warping, like the basement, may have no exits at all.
    private void ConnectExit(string exitName, Room destination, Vector2 direction)
    {
        Area2D exit = GetNodeOrNull<Area2D>(exitName);
        if (exit == null)
        {
            return;
        }

        exit.BodyEntered += body =>
        {
            if (destination != null && body.IsInGroup(Constants.PLAYER_GROUP))
            {
                GameSignals.Instance.EmitSignal(GameSignals.SignalName.RoomExitEntered, destination, body, direction);
            }
        };

        AddEnemyBarrier(exit);
    }

	// A wall only enemies collide with, over the exit, so they can't wander out through open doorways.
	private void AddEnemyBarrier(Area2D exit)
	{
		var barrier = new StaticBody2D
		{
			Name = exit.Name + "EnemyBarrier",
			Position = exit.Position,
			CollisionLayer = Constants.ENEMY_NO_ESCAPE_LAYER,
			CollisionMask = 0,
		};

		foreach (Node child in exit.GetChildren())
		{
			if (child is CollisionShape2D shape)
			{
				barrier.AddChild(shape.Duplicate());
			}
		}

		CallDeferred(Node.MethodName.AddChild, barrier);
	}
}
