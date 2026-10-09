using Godot;

public static class Constants
{
    // One room fills exactly one NES screen.
    public static readonly Vector2I SCREEN_SIZE = new(256, 176);

    public const int TILE_SIZE = 16;

    // How far into the next room the player is placed after a room transition.
    public const float ROOM_ENTRY_INSET = 2.5f * TILE_SIZE;

    // The walkable floor of a standard room, in room-local coordinates (inside the outer walls).
    public static readonly Rect2 ROOM_INTERIOR = new(32, 40, 192, 104);

    // Rupee and key counts cap here, and cheat mode pins them to it.
    public const int MAX_COLLECTABLE = 9999;

    public const int MAX_BOMBS = 8;

    // The player character joins this group so world objects like exits and doors can recognise him
    // without type-checking.
    public const string PLAYER_GROUP = "player";

    // Every enemy joins this group so rooms can count what is left to defeat.
    public const string ENEMY_GROUP = "enemy";

    // Collision layer bits, matching the layer names in Project Settings.
    public const uint WALLS_LAYER = 1 << 0;
    public const uint PLAYER_ATTACK_LAYER = 1 << 1;
    public const uint ENEMY_HURTBOX_LAYER = 1 << 2;
    public const uint COLLECTIBLE_LAYER = 1 << 3;
    public const uint ENEMY_NO_ESCAPE_LAYER = 1 << 7;
    public const uint ENEMY_ATTACK_LAYER = 1 << 8;
    public const uint PLAYER_HURTBOX_LAYER = 1 << 9;
    public const uint LADDER_LAYER = 1 << 10;
}
