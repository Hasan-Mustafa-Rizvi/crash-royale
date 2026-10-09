using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrashRoyale.Audio
{
    /// <summary>Sound effect ids used across the game.</summary>
    public static class Sfx
    {
        public const string Deploy = "deploy";
        public const string Hit = "hit";
        public const string SwordHit = "sword";
        public const string ArrowShoot = "arrow_shoot";
        public const string ArrowHit = "arrow_hit";
        public const string Throw = "throw";
        public const string Explosion = "explosion";
        public const string TowerShot = "tower_shot";
        public const string TowerDestroyed = "tower_destroyed";
        public const string UnitDeath = "unit_death";
        public const string Click = "click";
        public const string CardSelect = "card_select";
        public const string Invalid = "invalid";
        public const string Coin = "coin";
        public const string Purchase = "purchase";
        public const string Crown = "crown";
        public const string Emote = "emote";
        public const string Countdown = "countdown";
        public const string BattleStart = "battle_start";
        public const string DoubleElixir = "double_elixir";
        public const string Overtime = "overtime";
        public const string Victory = "victory";
        public const string Defeat = "defeat";
        public const string Draw = "draw";
    }

    public enum MusicTrack
    {
        None,
        Menu,
        Battle
    }

    [CreateAssetMenu(menuName = "Crash Royale/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        public const string ResourcePath = "SoundLibrary";

        [Serializable]
        public class Entry
        {
            public string id;
            public AudioClip[] clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float volume = 1f;
            public float pitchMin = 0.95f;
            public float pitchMax = 1.05f;
            [Tooltip("Minimum seconds between two plays of this sound, so twenty arrows don't stack into noise.")]
            public float minInterval = 0.04f;
        }

        public List<Entry> entries = new List<Entry>();
        public AudioClip menuMusic;
        public AudioClip battleMusic;
        [Range(0f, 1f)] public float musicGain = 0.8f;

        public Entry Find(string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == id) return entries[i];
            }

            return null;
        }
    }
}
