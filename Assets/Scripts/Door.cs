using Godot;

// A doorway blocker drawn and collided with as tiles. Opening it hides the tiles and their collision.
[GlobalClass]
public partial class Door : TileMapLayer
{
    [Export] public bool StartsOpen { get; set; }

    public bool IsOpen { get; private set; }

    private AudioStreamPlayer soundPlayer;

    public override void _Ready()
    {
        if (StartsOpen)
        {
            Open();
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

    public void Open()
    {
        IsOpen = true;

        // Deferred because doors usually open from inside physics callbacks.
        SetDeferred(TileMapLayer.PropertyName.Enabled, false);

        soundPlayer?.Play();
    }

    public void Close()
    {
        IsOpen = false;
        SetDeferred(TileMapLayer.PropertyName.Enabled, true);
    }
}
