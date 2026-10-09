using Godot;

// Values are stored by index in scenes and resources, and used as the order the player cycles through
// them, so only append new members.
public enum AltWeapon
{
    BOW,
    BOOMERANG,
    BOMB,
}

public enum AmmoType
{
    NONE,
    RUPEES,
    BOMBS,
}

// Data for an alternate weapon: what it spawns, what it costs, and how it is aimed. Entities fire these
// through a WeaponHolder.
[GlobalClass]
public partial class Weapon : Resource
{
    [Export] public AltWeapon Kind { get; set; }
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }

    // Must have a Projectile root.
    [Export] public PackedScene Scene { get; set; }

    [ExportGroup("Ammo")]
    [Export] public AmmoType Ammo { get; set; }
    [Export] public int AmmoCost { get; set; } = 1;

    [ExportGroup("Firing")]
    // How many of this weapon's projectiles can be alive at once. 0 means no limit.
    [Export] public int MaxActive { get; set; } = 1;

    // How far in front of the shooter the projectile appears.
    [Export] public float SpawnDistance { get; set; } = 8f;

    // Fires this many projectiles per shot, fanned out SpreadAngle degrees apart.
    [Export] public int SpreadCount { get; set; } = 1;
    [Export] public float SpreadAngle { get; set; }

    // Aims at the player character instead of the shooter's facing direction.
    [Export] public bool AimAtTarget { get; set; }
}
