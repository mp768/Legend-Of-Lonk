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

    // Various sound effects that are common to specific objects (and do not need to be selected via the editor).
    public const string DOOR_OPEN_SOUND_EFFECT_UID = "uid://b38b8h3yi0vai";

    public const string PLAYER_HURT_SOUND_EFFECT_UID = "uid://catk5hjqs0v65";
    public const string ENEMY_HURT_SOUND_EFFECT_UID = "uid://c05raosmw2dnv";
    public const string BOSS_HURT_SOUND_EFFECT_UID = "uid://dxxnd3rch7rh4";

    public const string PLAYER_DEATH_SOUND_EFFECT_UID = "uid://pwn6ewt8tkcq";
    public const string ENEMY_DEATH_SOUND_EFFECT_UID = "uid://c4fs2n12cl1i0";
    public const string BOSS_DEATH_SOUND_EFFECT_UID = "uid://cs4cr3i3bbifs";

    public const string SWORD_SWING_SOUND_EFFECT_UID = "uid://d3is5f1y6eou5";
    public const string SWORD_HIT_SOUND_EFFECT_UID = "uid://bbr43rjdswfkn";
    public const string SWORD_BEAM_SOUND_EFFECT_UID = "uid://bmwl4w38e58tj";
    public const string BOOMERANG_SOUND_EFFECT_UID = "uid://dqh28p3fu16tp";
    // The arrow shares the boomerang's clip, as on the NES.
    public const string ARROW_SOUND_EFFECT_UID = BOOMERANG_SOUND_EFFECT_UID;
    public const string BOMB_PLACE_SOUND_EFFECT_UID = "uid://dn45koihfo77i";
    public const string EXPLOSION_SOUND_EFFECT_UID = "uid://blxvdarunphbc";
    public const string FIREBALL_SOUND_EFFECT_UID = "uid://cu6vdft8ajtwb";

    public const string RUPEE_COLLECT_SOUND_EFFECT_UID = "uid://dbwo67s5xn5sf";
    public const string KEY_COLLECT_SOUND_EFFECT_UID = "uid://cwsejud2b50od";
    public const string REGEN_COLLECT_SOUND_EFFECT_UID = "uid://c8v0mnw0a2l4u";
    public const string ITEM_COLLECT_SOUND_EFFECT_UID = "uid://bhwaggx7ngk7f";
    public const string HEART_CONTAINER_COLLECT_SOUND_EFFECT_UID = "uid://ognnflc2hq1j";

    // Audio buses, matching the bus names in the Audio tab.
    public const string SFX_BUS = "SFX";
    public const string MUSIC_BUS = "Music";

    // The raw clips are much louder than the game should be, so every common sound effect is turned down by this.
    public const float SOUND_EFFECT_VOLUME_DB = -12f;

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
