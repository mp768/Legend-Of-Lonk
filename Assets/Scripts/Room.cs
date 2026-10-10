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

	// Emitted once, when the last enemy that counts toward clearing the room is defeated.
	[Signal]
	public delegate void ClearedEventHandler();

	[Export] public Room leftRoom;
	[Export] public Room rightRoom;
	[Export] public Room upRoom;
	[Export] public Room downRoom;

	[Export] public RoomView View { get; set; } = RoomView.TOP_DOWN;

	[ExportGroup("Clearing")]
	// Closes the room's shutter doors while enemies remain.
    [Export] public bool LockUntilCleared { get; set; }

    // Appears once when the room is cleared.
    [Export] public PackedScene ClearReward { get; set; }

	// Where the reward appears. If not set, it'll just appear in the center of the room.
	[Export] public Marker2D RewardSpawn { get; set; }

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

		if (ClearReward?.Instantiate() is Node2D reward)
		{
			reward.Position = RewardSpawn?.Position ?? (Vector2)Constants.SCREEN_SIZE / 2;

			// Usually reached from an enemy's death inside a physics callback.
            CallDeferred(Node.MethodName.AddChild, reward);
        }

        EmitSignal(SignalName.Cleared);
    }

    private void SetShuttersOpen(bool open)
    {
        foreach (Node child in GetChildren())
        {
            if (child is ShutterDoor shutter)
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
