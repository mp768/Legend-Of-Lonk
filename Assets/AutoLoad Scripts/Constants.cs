using Godot;

public static class Constants
{
    // One room fills exactly one NES screen.
    public static readonly Vector2I SCREEN_SIZE = new(256, 176);

    public const int TILE_SIZE = 16;

    // How far into the next room the player is placed after a room transition.
    public const float ROOM_ENTRY_INSET = 2.5f * TILE_SIZE;

    // Rupee and key counts cap here, and cheat mode pins them to it.
    public const int MAX_COLLECTABLE = 9999;

    // The player character joins this group so world objects like exits and doors can recognise him
    // without type-checking.
    public const string PLAYER_GROUP = "player";
}
