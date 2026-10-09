using Godot;

// What an enemy may leave behind when defeated. At most one item drops, as in the NES.
[GlobalClass]
public partial class DropTable : Resource
{
    // Chances are exact: a 0.4 entry drops 40% of the time. Whatever is left of 1 means no drop.
    [Export] public DropEntry[] Entries { get; set; } = [];

    // Returns the item to drop, or null for nothing.
    public PackedScene Roll()
    {
        float total = 0f;
        foreach (DropEntry entry in Entries)
        {
            total += entry?.Chance ?? 0f;
        }
        if (total > 1f + Mathf.Epsilon)
        {
            GD.PushWarning($"Drop chances in {ResourcePath} add up to {total}, more than 1. Later entries will drop less often than listed.");
        }

        // Roll once and walk the cumulative chances.
        float roll = GD.Randf();
        float cumulative = 0f;
        foreach (DropEntry entry in Entries)
        {
            if (entry == null)
            {
                continue;
            }

            cumulative += entry.Chance;
            if (roll < cumulative)
            {
                return entry.Item;
            }
        }

        return null;
    }
}
