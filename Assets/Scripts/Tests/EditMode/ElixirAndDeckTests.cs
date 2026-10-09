using System.Collections.Generic;
using CrashRoyale.Battle;
using CrashRoyale.Data;
using NUnit.Framework;
using UnityEngine;

namespace CrashRoyale.Tests
{
    public class ElixirBankTests
    {
        [Test]
        public void Regenerates_AndCapsAtTen()
        {
            var bank = new ElixirBank(5f, 0.5f);
            bank.Tick(4f, 1f);
            Assert.AreEqual(7f, bank.Current, 0.0001f);

            bank.Tick(100f, 1f);
            Assert.AreEqual(ElixirBank.Max, bank.Current, 0.0001f);
            Assert.Greater(bank.Wasted, 0f);
        }

        [Test]
        public void DoubleElixir_RegeneratesTwiceAsFast()
        {
            var normal = new ElixirBank(0f, 0.5f);
            var doubled = new ElixirBank(0f, 0.5f);
            normal.Tick(2f, 1f);
            doubled.Tick(2f, 2f);
            Assert.AreEqual(normal.Current * 2f, doubled.Current, 0.0001f);
        }

        [Test]
        public void Spend_FailsWhenTooPoor_AndDeductsOtherwise()
        {
            var bank = new ElixirBank(3f, 0.5f);
            Assert.IsFalse(bank.TrySpend(4));
            Assert.AreEqual(3f, bank.Current, 0.0001f);

            Assert.IsTrue(bank.TrySpend(3));
            Assert.AreEqual(0f, bank.Current, 0.0001f);
        }
    }

    public class DeckCycleTests
    {
        private readonly List<CardData> created = new List<CardData>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var c in created) Object.DestroyImmediate(c);
            created.Clear();
        }

        private CardData Card(string id)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = id;
            created.Add(c);
            return c;
        }

        [Test]
        public void Hand_HasThreeCards_AndNextIsTheFourth()
        {
            var deck = new DeckCycle(new[] { Card("a"), Card("b"), Card("c"), Card("d") }, 3, null);
            Assert.AreEqual(3, deck.HandSize);
            Assert.AreEqual("d", deck.Next.id);
        }

        [Test]
        public void PlayedCard_GoesToTheBack_AndCannotBePlayedTwiceInARow()
        {
            var deck = new DeckCycle(new[] { Card("a"), Card("b"), Card("c"), Card("d") }, 3, null);
            var played = deck.Play(0);

            Assert.AreEqual("a", played.id);
            Assert.AreEqual("d", deck.Hand(0).id, "The next card takes the empty slot");
            Assert.AreEqual("a", deck.Next.id, "The played card waits at the back of the queue");
            Assert.AreEqual(-1, deck.IndexOf(played));
        }

        [Test]
        public void Shuffle_KeepsEveryCardExactlyOnce()
        {
            var cards = new[] { Card("a"), Card("b"), Card("c"), Card("d") };
            var deck = new DeckCycle(cards, 3, new System.Random(42));
            var seen = new HashSet<string> { deck.Next.id };
            for (int i = 0; i < deck.HandSize; i++) seen.Add(deck.Hand(i).id);
            Assert.AreEqual(4, seen.Count);
        }
    }
}
