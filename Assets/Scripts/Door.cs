using Godot;

// A doorway blocker drawn and collided with as tiles. Opening it hides the tiles and their collision.
[GlobalClass]
public partial class Door : TileMapLayer
{
    [Export] public bool StartsOpen { get; set; }

    public bool IsOpen { get; private set; }

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
    }

    public void Open()
    {
        IsOpen = true;

        // Deferred because doors usually open from inside physics callbacks.
        SetDeferred(TileMapLayer.PropertyName.Enabled, false);
    }

    public void Close()
    {
        IsOpen = false;
        SetDeferred(TileMapLayer.PropertyName.Enabled, true);
    }
}
