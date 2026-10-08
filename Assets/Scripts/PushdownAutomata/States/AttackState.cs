using Godot;

// Holds a directional attack pose for a fixed time. Sword attacks also enable the hitbox facing that way.
[GlobalClass]
public partial class AttackState : State
{
	public enum AttackKind
	{
		SWORD,
		ITEM,
	}

	[ExportGroup("Weapon Hitboxes")]
	[Export] private Affectables swordLeft;
	[Export] private Affectables swordRight;
	[Export] private Affectables swordDown;
	[Export] private Affectables swordUp;

	[ExportGroup("Pose")]
	[Export] public string AnimHorizontal { get; set; } = "sword_horizontal";
	[Export] public string AnimDown { get; set; } = "sword_down";
	[Export] public string AnimUp { get; set; } = "sword_up";

	// Sprite offsets for each pose. The horizontal offset is for facing right, and it is mirrored for left.
	[Export] public Vector2I HorizontalSwordOffset { get; set; } = new(6, -2);
	[Export] public Vector2I UpOffset { get; set; } = new(0, -10);
	[Export] public Vector2I DownOffset { get; set; } = new(0, 5);

	[Export] public float Duration { get; set; } = 0.5f;

	private AttackKind kind;
	private float timer;

	public override void Initialize(PushdownStateMachine machine)
	{
		base.Initialize(machine);

		// Knock targets back in the direction of the swing.
		if (swordLeft != null) swordLeft.Direction = Vector2.Left;
		if (swordRight != null) swordRight.Direction = Vector2.Right;
		if (swordUp != null) swordUp.Direction = Vector2.Up;
		if (swordDown != null) swordDown.Direction = Vector2.Down;

		DisableAllHitboxes();
	}

	public void Configure(AttackKind attackKind)
	{
		kind = attackKind;
	}

	public override void Enter()
	{
		timer = Duration;
		BeginSwing();
	}

	public override void PhysicsUpdate(double delta)
	{
		timer -= (float)delta;
		if (timer <= 0f)
		{
			Machine.PopState();
		}
	}

	// Interrupted (e.g. stunned), so retract the weapon until the attack resumes.
	public override void Pause()
	{
		DisableAllHitboxes();
	}

	public override void Resume()
	{
		BeginSwing();
	}

	public override void Exit()
	{
		DisableAllHitboxes();
		Visuals?.ClearPoseOffset();
	}

	private void BeginSwing()
	{
		Vector2 facing = Entity.FacingDirection;
		bool isSword = kind == AttackKind.SWORD;

		// Frame 1 of each attack animation shows the sword extended, and frame 0 is the throwing pose.
		int frame = isSword ? 1 : 0;

		string animation;
		bool flipH = false;
		Vector2I? offset;
		Affectables hitbox;

		if (facing.X != 0f)
		{
			animation = AnimHorizontal;
			flipH = facing.X < 0f;
			hitbox = flipH ? swordLeft : swordRight;

			// Only the extended sword shifts the sprite sideways. The throwing pose uses the resting offset.
			offset = isSword ? new Vector2I(flipH ? -HorizontalSwordOffset.X : HorizontalSwordOffset.X, HorizontalSwordOffset.Y) : null;
		}
		else if (facing.Y < 0f)
		{
			animation = AnimUp;
			offset = UpOffset;
			hitbox = swordUp;
		}
		else
		{
			animation = AnimDown;
			offset = DownOffset;
			hitbox = swordDown;
		}

		DisableAllHitboxes();
		if (isSword)
		{
			SetHitboxActive(hitbox, true);
		}

		Visuals?.ShowPose(animation, frame, flipH, offset);
	}

	private void DisableAllHitboxes()
	{
		SetHitboxActive(swordLeft, false);
		SetHitboxActive(swordRight, false);
		SetHitboxActive(swordUp, false);
		SetHitboxActive(swordDown, false);
	}

	private static void SetHitboxActive(Affectables hitbox, bool active)
	{
		if (hitbox == null)
		{
			return;
		}

		// Deferred because attacks can start or stop inside physics callbacks.
		hitbox.SetDeferred(Area2D.PropertyName.Monitorable, active);
		hitbox.SetDeferred(Area2D.PropertyName.Monitoring, active);
	}
}
