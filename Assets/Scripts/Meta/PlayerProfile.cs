using System;
using System.Collections.Generic;
using CrashRoyale.Battle;
using CrashRoyale.Data;
using UnityEngine;

namespace CrashRoyale.Meta
{
    public struct Reward
    {
        public int Gold;
        public int Trophies;
    }

    /// <summary>The local save: name, gold, trophies, unlocked cards, battle deck, emotes and settings.</summary>
    public static class PlayerProfile
    {
        public const int DeckSize = 4;
        public const int StartingGold = 150;
        public const int MaxNameLength = 16;

        private const string Prefix = "cr.";
        private const string KeyInitialized = Prefix + "initialized";
        private const string KeyName = Prefix + "name";
        private const string KeyGold = Prefix + "gold";
        private const string KeyTrophies = Prefix + "trophies";
        private const string KeyBestTrophies = Prefix + "bestTrophies";
        private const string KeyWins = Prefix + "wins";
        private const string KeyLosses = Prefix + "losses";
        private const string KeyDraws = Prefix + "draws";
        private const string KeyDeck = Prefix + "deck";
        private const string KeyUnlocked = Prefix + "unlocked";
        private const string KeyEmotes = Prefix + "emotes";
        private const string KeyDifficulty = Prefix + "difficulty";
        private const string KeyMusic = Prefix + "musicVolume";
        private const string KeySfx = Prefix + "sfxVolume";

        private static IKeyValueStore store = new PlayerPrefsStore();

        public static event Action Changed;

        /// <summary>Swap the backing store (tests use <see cref="MemoryStore"/> so they never touch real saves).</summary>
        public static IKeyValueStore Store
        {
            get => store;
            set => store = value ?? new PlayerPrefsStore();
        }

        public static string Name
        {
            get => store.GetString(KeyName, string.Empty);
            set => SetString(KeyName, SanitizeName(value));
        }

        public static bool HasName => !string.IsNullOrWhiteSpace(Name);
        public static int Gold => store.GetInt(KeyGold, StartingGold);
        public static int Trophies => store.GetInt(KeyTrophies, 0);
        public static int BestTrophies => store.GetInt(KeyBestTrophies, 0);
        public static int Wins => store.GetInt(KeyWins, 0);
        public static int Losses => store.GetInt(KeyLosses, 0);
        public static int Draws => store.GetInt(KeyDraws, 0);

        public static Difficulty Difficulty
        {
            get => (Difficulty)Mathf.Clamp(store.GetInt(KeyDifficulty, (int)Difficulty.Normal), 0, 2);
            set => SetInt(KeyDifficulty, (int)value);
        }

        public static float MusicVolume
        {
            get => store.GetFloat(KeyMusic, 0.6f);
            set => SetFloat(KeyMusic, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => store.GetFloat(KeySfx, 0.8f);
            set => SetFloat(KeySfx, Mathf.Clamp01(value));
        }

        public static IReadOnlyList<string> Deck => Split(store.GetString(KeyDeck, string.Empty));

        /// <summary>Creates a fresh save on first launch and repairs a deck that references missing or locked cards.</summary>
        public static void EnsureInitialized(CardDatabase database)
        {
            if (database == null) return;

            if (!store.HasKey(KeyInitialized))
            {
                store.SetInt(KeyInitialized, 1);
                store.SetInt(KeyGold, StartingGold);
                store.SetString(KeyDeck, string.Join(",", database.defaultDeck));
                store.Save();
            }

            if (!IsValidDeck(Deck, database))
            {
                store.SetString(KeyDeck, string.Join(",", database.defaultDeck));
                store.Save();
            }
        }

        public static bool IsValidDeck(IReadOnlyList<string> ids, CardDatabase database)
        {
            if (ids == null || ids.Count != DeckSize) return false;

            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                var card = database.Get(id);
                if (card == null || !IsUnlocked(card) || !seen.Add(id)) return false;
            }

            return true;
        }

        public static bool IsUnlocked(CardData card)
        {
            return card != null && (card.unlockedByDefault || Contains(KeyUnlocked, card.id));
        }

        public static void Unlock(string cardId) => AddToList(KeyUnlocked, cardId);

        public static bool OwnsEmote(EmoteData emote) => emote != null && (emote.price <= 0 || Contains(KeyEmotes, emote.id));

        public static void GrantEmote(string emoteId) => AddToList(KeyEmotes, emoteId);

        public static List<EmoteData> OwnedEmotes(CardDatabase database)
        {
            var result = new List<EmoteData>();
            if (database == null) return result;
            foreach (var emote in database.emotes)
            {
                if (OwnsEmote(emote)) result.Add(emote);
            }

            return result;
        }

        public static bool SetDeck(IReadOnlyList<string> ids, CardDatabase database)
        {
            if (!IsValidDeck(ids, database)) return false;
            SetString(KeyDeck, string.Join(",", ids));
            return true;
        }

        public static void AddGold(int amount) => SetInt(KeyGold, Mathf.Max(0, Gold + amount));

        public static bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            SetInt(KeyGold, Gold - amount);
            return true;
        }

        public static Reward ApplyBattleResult(BattleResult result, Difficulty difficulty)
        {
            var reward = CalculateReward(result, difficulty);

            switch (result.Outcome)
            {
                case BattleOutcome.BlueWins:
                    store.SetInt(KeyWins, Wins + 1);
                    break;
                case BattleOutcome.RedWins:
                    store.SetInt(KeyLosses, Losses + 1);
                    break;
                case BattleOutcome.Draw:
                    store.SetInt(KeyDraws, Draws + 1);
                    break;
            }

            int trophies = Mathf.Max(0, Trophies + reward.Trophies);
            store.SetInt(KeyTrophies, trophies);
            if (trophies > BestTrophies) store.SetInt(KeyBestTrophies, trophies);
            store.SetInt(KeyGold, Mathf.Max(0, Gold + reward.Gold));
            store.Save();
            Changed?.Invoke();
            return reward;
        }

        public static Reward CalculateReward(BattleResult result, Difficulty difficulty)
        {
            float goldScale = difficulty == Difficulty.Easy ? 0.6f : difficulty == Difficulty.Hard ? 1.5f : 1f;
            int win = difficulty == Difficulty.Easy ? 20 : difficulty == Difficulty.Hard ? 40 : 30;
            int loss = difficulty == Difficulty.Easy ? 10 : difficulty == Difficulty.Hard ? 25 : 20;

            switch (result.Outcome)
            {
                case BattleOutcome.BlueWins:
                    return new Reward { Gold = Mathf.RoundToInt((100 + 20 * result.BlueCrowns) * goldScale), Trophies = win };
                case BattleOutcome.RedWins:
                    return new Reward { Gold = Mathf.RoundToInt((15 + 10 * result.BlueCrowns) * goldScale), Trophies = -Mathf.Min(loss, Trophies) };
                case BattleOutcome.Draw:
                    return new Reward { Gold = Mathf.RoundToInt(40 * goldScale), Trophies = 0 };
                default:
                    return default;
            }
        }

        public static void ResetProgress()
        {
            string[] keys =
            {
                KeyInitialized, KeyName, KeyGold, KeyTrophies, KeyBestTrophies, KeyWins, KeyLosses, KeyDraws,
                KeyDeck, KeyUnlocked, KeyEmotes, KeyDifficulty
            };

            foreach (var key in keys) store.DeleteKey(key);
            store.Save();
            Changed?.Invoke();
        }

        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var chars = new List<char>(raw.Length);
            foreach (char c in raw.Trim())
            {
                // ASCII only: the UI font ships a Latin atlas.
                bool letterOrDigit = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (letterOrDigit || c == ' ' || c == '_' || c == '-') chars.Add(c);
                if (chars.Count >= MaxNameLength) break;
            }

            return new string(chars.ToArray()).Trim();
        }

        private static bool Contains(string key, string id)
        {
            foreach (var entry in Split(store.GetString(key, string.Empty)))
            {
                if (entry == id) return true;
            }

            return false;
        }

        private static void AddToList(string key, string id)
        {
            if (string.IsNullOrEmpty(id) || Contains(key, id)) return;
            string current = store.GetString(key, string.Empty);
            SetString(key, string.IsNullOrEmpty(current) ? id : current + "," + id);
        }

        private static List<string> Split(string csv)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(csv)) return result;
            foreach (var part in csv.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }

            return result;
        }

        private static void SetInt(string key, int value)
        {
            store.SetInt(key, value);
            store.Save();
            Changed?.Invoke();
        }

        private static void SetFloat(string key, float value)
        {
            store.SetFloat(key, value);
            store.Save();
            Changed?.Invoke();
        }

        private static void SetString(string key, string value)
        {
            store.SetString(key, value);
            store.Save();
            Changed?.Invoke();
        }
    }
}
