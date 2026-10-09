using System;
using System.Collections.Generic;
using CrashRoyale.Data;

namespace CrashRoyale.Battle
{
    /// <summary>
    /// Clash-style card cycle: a few cards in hand, the rest queued. A played card goes to the back of the queue,
    /// so you can never play the same card twice in a row.
    /// </summary>
    public class DeckCycle
    {
        private readonly CardData[] hand;
        private readonly List<CardData> queue;
        private readonly List<CardData> all;

        public DeckCycle(IList<CardData> cards, int handSize, Random rng)
        {
            var list = new List<CardData>();
            foreach (var card in cards)
            {
                if (card != null) list.Add(card);
            }

            if (list.Count == 0) throw new ArgumentException("A deck needs at least one card.", nameof(cards));
            all = new List<CardData>(list);

            if (rng != null)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }

            int size = Math.Max(1, Math.Min(handSize, list.Count));
            hand = list.GetRange(0, size).ToArray();
            queue = list.GetRange(size, list.Count - size);
        }

        public event Action Changed;

        public int HandSize => hand.Length;

        /// <summary>Every card in the deck, in no particular order.</summary>
        public IReadOnlyList<CardData> Cards => all;
        public CardData Next => queue.Count > 0 ? queue[0] : null;

        public CardData Hand(int index) => index >= 0 && index < hand.Length ? hand[index] : null;

        public int IndexOf(CardData card) => Array.IndexOf(hand, card);

        public CardData Play(int index)
        {
            var played = Hand(index);
            if (played == null) return null;

            if (queue.Count > 0)
            {
                hand[index] = queue[0];
                queue.RemoveAt(0);
                queue.Add(played);
            }

            Changed?.Invoke();
            return played;
        }
    }
}
