using Godot;
using System.Collections.Generic;

// Plays the sound effects shared by the player, enemies and collectibles. It lives outside the rooms, so a
// clip outlives whatever triggered it: pickups and enemies free themselves on contact, rooms get disabled,
// and the whole scene reloads on death.
public partial class SoundPlayer : Node
{
    // Values are stored by index in scenes, so only append new members.
    public enum HurtSound { PLAYER, ENEMY, BOSS }
    public enum DeathSound { PLAYER, ENEMY, BOSS }
    public enum AttackSound { NONE, SWORD_SWING, SWORD_HIT, SWORD_BEAM, ARROW, BOOMERANG, BOMB_PLACE, EXPLOSION, FIREBALL }
    public enum CollectSound { RUPEE, KEY, REGEN, ITEM, HEART_CONTAINER }

    public static SoundPlayer Instance { get; private set; }

    private const float SWORD_SWING_VOLUME_DB = -12f;
    private const float PROJECTILE_VOLUME_DB = -3f;

    // Attack clips that sit too loud against the rest, and how far each is turned down on top of the overall
    // sound effect volume.
    private static readonly Dictionary<AttackSound, float> attackVolumesDb = new()
    {
        [AttackSound.SWORD_SWING] = SWORD_SWING_VOLUME_DB,
        [AttackSound.SWORD_BEAM] = PROJECTILE_VOLUME_DB,
        [AttackSound.ARROW] = PROJECTILE_VOLUME_DB,
        [AttackSound.BOOMERANG] = PROJECTILE_VOLUME_DB,
        [AttackSound.BOMB_PLACE] = PROJECTILE_VOLUME_DB,
        [AttackSound.FIREBALL] = PROJECTILE_VOLUME_DB,
    };

    private Dictionary<HurtSound, AudioStream> hurtSounds;
    private Dictionary<DeathSound, AudioStream> deathSounds;
    private Dictionary<AttackSound, AudioStream> attackSounds;
    private Dictionary<CollectSound, AudioStream> collectSounds;

    private AudioStreamPlaybackPolyphonic playback;

    // The clips started on `lastPlayFrame` and the handle each one got, so the same one isn't stacked on itself.
    private readonly Dictionary<AudioStream, long> playedThisFrame = [];
    private ulong lastPlayFrame;

    // How many callers of StartAttack still hold each clip. A clip shared by several, like a volley's, ends
    // with the last of them.
    private readonly Dictionary<long, int> holders = [];

    public override void _Ready()
    {
        Instance = this;

        hurtSounds = new()
        {
            [HurtSound.PLAYER] = LoadClip(Constants.PLAYER_HURT_SOUND_EFFECT_UID),
            [HurtSound.ENEMY] = LoadClip(Constants.ENEMY_HURT_SOUND_EFFECT_UID),
            [HurtSound.BOSS] = LoadClip(Constants.BOSS_HURT_SOUND_EFFECT_UID),
        };

        deathSounds = new()
        {
            [DeathSound.PLAYER] = LoadClip(Constants.PLAYER_DEATH_SOUND_EFFECT_UID),
            [DeathSound.ENEMY] = LoadClip(Constants.ENEMY_DEATH_SOUND_EFFECT_UID),
            [DeathSound.BOSS] = LoadClip(Constants.BOSS_DEATH_SOUND_EFFECT_UID),
        };

        // NONE has no entry, so it plays nothing.
        attackSounds = new()
        {
            [AttackSound.SWORD_SWING] = LoadClip(Constants.SWORD_SWING_SOUND_EFFECT_UID),
            [AttackSound.SWORD_HIT] = LoadClip(Constants.SWORD_HIT_SOUND_EFFECT_UID),
            [AttackSound.SWORD_BEAM] = LoadClip(Constants.SWORD_BEAM_SOUND_EFFECT_UID),
            [AttackSound.ARROW] = LoadClip(Constants.ARROW_SOUND_EFFECT_UID),
            [AttackSound.BOOMERANG] = LoadClip(Constants.BOOMERANG_SOUND_EFFECT_UID),
            [AttackSound.BOMB_PLACE] = LoadClip(Constants.BOMB_PLACE_SOUND_EFFECT_UID),
            [AttackSound.EXPLOSION] = LoadClip(Constants.EXPLOSION_SOUND_EFFECT_UID),
            [AttackSound.FIREBALL] = LoadClip(Constants.FIREBALL_SOUND_EFFECT_UID),
        };

        collectSounds = new()
        {
            [CollectSound.RUPEE] = LoadClip(Constants.RUPEE_COLLECT_SOUND_EFFECT_UID),
            [CollectSound.KEY] = LoadClip(Constants.KEY_COLLECT_SOUND_EFFECT_UID),
            [CollectSound.REGEN] = LoadClip(Constants.REGEN_COLLECT_SOUND_EFFECT_UID),
            [CollectSound.ITEM] = LoadClip(Constants.ITEM_COLLECT_SOUND_EFFECT_UID),
            [CollectSound.HEART_CONTAINER] = LoadClip(Constants.HEART_CONTAINER_COLLECT_SOUND_EFFECT_UID),
        };

        var player = new AudioStreamPlayer
        {
            Stream = new AudioStreamPolyphonic(),
            Bus = Constants.SFX_BUS,
            VolumeDb = Constants.SOUND_EFFECT_VOLUME_DB,
        };

        AddChild(player);
        player.Play();

        playback = (AudioStreamPlaybackPolyphonic)player.GetStreamPlayback();
    }

    public void PlayHurt(HurtSound sound) => Play(hurtSounds.GetValueOrDefault(sound));

    public void PlayDeath(DeathSound sound) => Play(deathSounds.GetValueOrDefault(sound));

    public void PlayAttack(AttackSound sound) => Play(attackSounds.GetValueOrDefault(sound), attackVolumesDb.GetValueOrDefault(sound));

    public void PlayCollect(CollectSound sound) => Play(collectSounds.GetValueOrDefault(sound));

    // Plays an attack clip that belongs to something that can disappear, like a projectile. Hand the returned
    // handle to Stop once that thing is gone, so the clip doesn't carry on without it.
    public long StartAttack(AttackSound sound)
    {
        long handle = Play(attackSounds.GetValueOrDefault(sound), attackVolumesDb.GetValueOrDefault(sound));
        if (handle != AudioStreamPlaybackPolyphonic.InvalidId)
        {
            holders[handle] = holders.GetValueOrDefault(handle) + 1;
        }

        return handle;
    }

    public void Stop(long handle)
    {
        if (!holders.TryGetValue(handle, out int count))
        {
            return;
        }

        if (count > 1)
        {
            holders[handle] = count - 1;
            return;
        }

        holders.Remove(handle);
        playback.StopStream(handle);
    }

    private static AudioStream LoadClip(string uid)
    {
        var clip = GD.Load<AudioStream>(uid);
        if (clip == null)
        {
            GD.PushError($"No sound effect could be loaded from {uid}.");
        }

        return clip;
    }

    private long Play(AudioStream clip, float volumeDb = 0f)
    {
        if (clip == null)
        {
            return AudioStreamPlaybackPolyphonic.InvalidId;
        }

        ulong frame = Engine.GetProcessFrames();
        if (frame != lastPlayFrame)
        {
            lastPlayFrame = frame;
            playedThisFrame.Clear();
        }

        if (!playedThisFrame.TryGetValue(clip, out long handle))
        {
            handle = playback.PlayStream(clip, volumeDb: volumeDb, bus: Constants.SFX_BUS);
            playedThisFrame[clip] = handle;
        }

        return handle;
    }
}
