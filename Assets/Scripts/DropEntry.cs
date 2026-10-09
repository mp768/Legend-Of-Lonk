using Godot;

// One possible drop in a DropTable.
[GlobalClass]
public partial class DropEntry : Resource
{
    [Export] public PackedScene Item { get; set; }

    // Chance out of 1 that a roll lands on this entry.
    [Export(PropertyHint.Range, "0,1,0.01")] public float Chance { get; set; }
}
