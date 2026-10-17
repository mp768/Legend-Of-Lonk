using Godot;

// A doorway blocker drawn and collided with as tiles. Opening it hides the tiles and their collision.
[GlobalClass]
public partial class Door : TileMapLayer
{
    [Signal]
    public delegate void OpenedEventHandler();

    [Export] public bool StartsOpen { get; set; }

    // Which wall of its room the door sits in. Set once in each door scene, so every instance inherits it.
    [Export] public Room.Side Side { get; set; }

    public bool IsOpen { get; private set; }

    private AudioStreamPlayer soundPlayer;

    public override void _Ready()
    {
        if (StartsOpen)
        {
            Open(playSound: false);
        }
        else
        {
            Close();
        }

        var doorOpenSoundEffect = GD.Load<AudioStream>(Constants.DOOR_OPEN_SOUND_EFFECT_UID);

        soundPlayer = new AudioStreamPlayer
        {
            Stream = doorOpenSoundEffect,
            VolumeDb = -12,
        };

        AddChild(soundPlayer);
    }

    public void Open(bool playSound = true)
    {
        IsOpen = true;

        // Deferred because doors usually open from inside physics callbacks.
        SetDeferred(TileMapLayer.PropertyName.Enabled, false);

        if (playSound)
        {
            soundPlayer?.Play();
        }

        EmitSignal(SignalName.Opened);
    }

    public void Close()
    {
        IsOpen = false;
        SetDeferred(TileMapLayer.PropertyName.Enabled, true);
    }
}
