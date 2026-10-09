using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrashRoyale.Data
{
    [Serializable]
    public class EmoteData
    {
        public string id;
        [Tooltip("Shown when there is no icon, and used as the label in the shop.")]
        public string text;
        public Sprite icon;
        [Min(0)] public int price;
    }

    /// <summary>All cards and emotes in the game. Lives in Resources so menus and battles can look cards up by id.</summary>
    [CreateAssetMenu(menuName = "Crash Royale/Card Database", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        public const string ResourcePath = "CardDatabase";

        public List<CardData> cards = new List<CardData>();
        public List<string> defaultDeck = new List<string>();
        public List<EmoteData> emotes = new List<EmoteData>();

        [Tooltip("Decks the AI picks from on Normal and Hard.")]
        public List<DeckPreset> aiDecks = new List<DeckPreset>();

        private static CardDatabase cached;

        public static CardDatabase Load()
        {
            if (cached == null) cached = Resources.Load<CardDatabase>(ResourcePath);
            return cached;
        }

        public CardData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].id == id) return cards[i];
            }

            return null;
        }

        public EmoteData GetEmote(string id)
        {
            for (int i = 0; i < emotes.Count; i++)
            {
                if (emotes[i].id == id) return emotes[i];
            }

            return null;
        }
    }

    [Serializable]
    public class DeckPreset
    {
        public List<string> cardIds = new List<string>();
    }
}
