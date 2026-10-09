using Godot;

// Types out requested dialogue one character at a time, and clears it when the player leaves the room.
public partial class DialogueBox : Label
{
	[Export] public float SecondsPerCharacter { get; set; } = 0.08f;

	private Timer typeTimer;

	public override void _Ready()
	{
		Text = "";
		VisibleCharacters = 0;

		typeTimer = new Timer { WaitTime = SecondsPerCharacter };
		typeTimer.Timeout += TypeNextCharacter;
		AddChild(typeTimer);

		GameSignals.Instance.DialogueRequested += OnDialogueRequested;
		GameSignals.Instance.RoomTransitionStarted += OnRoomTransitionStarted;
	}

	public override void _ExitTree()
	{
		typeTimer?.Stop();
		GameSignals.Instance.DialogueRequested -= OnDialogueRequested;
		GameSignals.Instance.RoomTransitionStarted -= OnRoomTransitionStarted;
	}

	private void OnDialogueRequested(string text)
	{
		Text = text;
		VisibleCharacters = 0;
		typeTimer.Start();
	}

	private void OnRoomTransitionStarted(Vector2 direction)
	{
		typeTimer.Stop();
		Text = "";
		VisibleCharacters = 0;
	}

	private void TypeNextCharacter()
	{
		VisibleCharacters++;
		if (VisibleCharacters >= Text.Length)
		{
			typeTimer.Stop();
		}
	}
}
