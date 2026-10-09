using System.Collections.Generic;
using CrashRoyale.Battle;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using NUnit.Framework;
using UnityEngine;

namespace CrashRoyale.Tests
{
    public class PlayerProfileTests
    {
        private CardDatabase database;
        private IKeyValueStore previous;

        [SetUp]
        public void SetUp()
        {
            previous = PlayerProfile.Store;
            PlayerProfile.Store = new MemoryStore();

            database = ScriptableObject.CreateInstance<CardDatabase>();
            foreach (var (id, unlocked) in new[] { ("a", true), ("b", true), ("c", true), ("d", true), ("e", false) })
            {
                var card = ScriptableObject.CreateInstance<CardData>();
                card.id = id;
                card.unlockedByDefault = unlocked;
                card.unlockPrice = 100;
                database.cards.Add(card);
            }

            database.defaultDeck = new List<string> { "a", "b", "c", "d" };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var c in database.cards) Object.DestroyImmediate(c);
            Object.DestroyImmediate(database);
            PlayerProfile.Store = previous;
        }

        [Test]
        public void FreshProfile_GetsStartingGoldAndDefaultDeck()
        {
            PlayerProfile.EnsureInitialized(database);
            Assert.AreEqual(PlayerProfile.StartingGold, PlayerProfile.Gold);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, PlayerProfile.Deck);
        }

        [Test]
        public void Deck_RejectsLockedDuplicateOrWrongSizedDecks()
        {
            PlayerProfile.EnsureInitialized(database);
            Assert.IsFalse(PlayerProfile.SetDeck(new[] { "a", "b", "c", "e" }, database), "locked card");
            Assert.IsFalse(PlayerProfile.SetDeck(new[] { "a", "a", "b", "c" }, database), "duplicate");
            Assert.IsFalse(PlayerProfile.SetDeck(new[] { "a", "b", "c" }, database), "too small");

            PlayerProfile.Unlock("e");
            Assert.IsTrue(PlayerProfile.SetDeck(new[] { "e", "b", "c", "d" }, database));
        }

        [Test]
        public void Purchases_NeedEnoughGold()
        {
            PlayerProfile.EnsureInitialized(database);
            Assert.IsFalse(PlayerProfile.TrySpendGold(PlayerProfile.Gold + 1));
            Assert.IsTrue(PlayerProfile.TrySpendGold(100));
            Assert.AreEqual(PlayerProfile.StartingGold - 100, PlayerProfile.Gold);
        }

        [Test]
        public void Losing_NeverTakesTrophiesBelowZero()
        {
            PlayerProfile.EnsureInitialized(database);
            var loss = new BattleResult { Outcome = BattleOutcome.RedWins };
            var reward = PlayerProfile.ApplyBattleResult(loss, Difficulty.Normal);
            Assert.AreEqual(0, reward.Trophies);
            Assert.AreEqual(0, PlayerProfile.Trophies);
            Assert.AreEqual(1, PlayerProfile.Losses);
        }

        [Test]
        public void Winning_PaysMoreOnHard()
        {
            var win = new BattleResult { Outcome = BattleOutcome.BlueWins, BlueCrowns = 3 };
            var normal = PlayerProfile.CalculateReward(win, Difficulty.Normal);
            var hard = PlayerProfile.CalculateReward(win, Difficulty.Hard);
            Assert.Greater(hard.Gold, normal.Gold);
            Assert.Greater(hard.Trophies, normal.Trophies);
        }

        [TestCase("  Hasan  ", "Hasan")]
        [TestCase("<b>bold</b>", "bboldb")]
        [TestCase("ThisNameIsWayTooLongForTheUI", "ThisNameIsWayToo")]
        public void Names_AreSanitized(string raw, string expected)
        {
            Assert.AreEqual(expected, PlayerProfile.SanitizeName(raw));
        }
    }

    public class ArenaLayoutTests
    {
        private GameObject go;
        private ArenaLayout layout;

        [SetUp]
        public void SetUp()
        {
            BattleRegistry.Clear();
            go = new GameObject("Arena");
            layout = go.AddComponent<ArenaLayout>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void EachSide_DeploysOnlyOnItsOwnHalf()
        {
            Assert.IsTrue(layout.IsDeployable(Team.Blue, new Vector2(0f, -2.5f)));
            Assert.IsFalse(layout.IsDeployable(Team.Blue, new Vector2(0f, 2.5f)));
            Assert.IsTrue(layout.IsDeployable(Team.Red, new Vector2(0f, 2.5f)));
            Assert.IsFalse(layout.IsDeployable(Team.Red, new Vector2(0f, -2.5f)));
        }

        [Test]
        public void River_AndOutsideTheField_AreNeverDeployable()
        {
            Assert.IsFalse(layout.IsDeployable(Team.Blue, new Vector2(0f, 0f)));
            Assert.IsFalse(layout.IsDeployable(Team.Blue, new Vector2(10f, -2f)));
        }

        [Test]
        public void CrossingTroops_RouteThroughTheNearestBridge()
        {
            int bridge = -1;
            var waypoint = layout.Steer(new Vector2(-3f, -3f), new Vector2(-2.8f, 4f), ref bridge);
            Assert.AreEqual(0, bridge, "left lane should use the left bridge");
            Assert.AreEqual(-layout.laneX, waypoint.x, 0.001f);
            Assert.Less(waypoint.y, 0f, "first waypoint is the bridge entrance on our side");
        }

        [Test]
        public void SameSideTargets_AreWalkedToDirectly()
        {
            int bridge = -1;
            var target = new Vector2(1f, -4f);
            Assert.AreEqual(target, layout.Steer(new Vector2(-1f, -2f), target, ref bridge));
            Assert.AreEqual(-1, bridge);
        }

        [Test]
        public void PathLength_AcrossTheRiver_IsLongerThanTheStraightLine()
        {
            var a = new Vector2(0f, -3f);
            var b = new Vector2(0f, 3f);
            Assert.Greater(layout.PathLength(a, b), Vector2.Distance(a, b));
        }
    }
}
