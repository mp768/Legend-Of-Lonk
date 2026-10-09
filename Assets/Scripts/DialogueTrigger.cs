using Godot;

// Shows a line of text whenever its room is entered, e.g. the old man's hint.
[GlobalClass]
public partial class DialogueTrigger : Node, IResettableEntity
{
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";

    public void OnRoomEntered()
    {
        GameSignals.Instance.EmitSignal(GameSignals.SignalName.DialogueRequested, Text);
    }
}
