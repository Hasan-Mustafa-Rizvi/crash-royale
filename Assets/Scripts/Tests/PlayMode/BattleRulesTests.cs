using System.Collections;
using CrashRoyale.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CrashRoyale.Tests
{
    /// <summary>Win, loss, crowns, overtime and deployment rules, driven by hand with the AI switched off.</summary>
    public class BattleRulesTests : BattleTestBase
    {
        [SetUp]
        public void SetUp()
        {
            BattleSettings.RedIsAI = false;
            BattleSettings.SkipIntro = true;
            BattleSettings.ApplyRewards = false;
            BattleSettings.Seed = 1234;
        }

        [UnityTest]
        public IEnumerator DestroyingTheEnemyKing_WinsImmediately_WithThreeCrowns()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;

            FindTower(Team.Red, true).ForceDestroy();
            yield return null;

            Assert.AreEqual(BattleState.Ended, battle.State);
            Assert.AreEqual(BattleOutcome.BlueWins, battle.Result.Outcome);
            Assert.AreEqual(3, battle.Crowns(Team.Blue));
            Assert.IsTrue(battle.Result.KingDestroyed);
        }

        [UnityTest]
        public IEnumerator LosingYourKing_IsADefeat()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;

            FindTower(Team.Blue, true).ForceDestroy();
            yield return null;

            Assert.AreEqual(BattleOutcome.RedWins, battle.Result.Outcome);
            Assert.AreEqual(3, battle.Crowns(Team.Red));
        }

        [UnityTest]
        public IEnumerator PrincessTower_GivesACrown_AndWakesTheKing()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;
            var king = FindTower(Team.Red, true);
            Assert.IsFalse(king.Active, "King starts asleep");

            FindTower(Team.Red, false, 0).ForceDestroy();
            yield return null;

            Assert.AreEqual(1, battle.Crowns(Team.Blue));
            Assert.IsTrue(king.Active, "King wakes up when a princess tower falls");
            Assert.IsTrue(battle.IsRunning, "One crown does not end the match");
            Assert.IsTrue(battle.Layout.PocketOpen(Team.Blue, 0), "Destroyed tower opens a pocket in that lane");
        }

        [UnityTest]
        public IEnumerator TimeUp_WithMoreCrowns_Wins()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;

            FindTower(Team.Blue, false, 1).ForceDestroy();
            battle.DebugSetTimeRemaining(0.05f);
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(BattleState.Ended, battle.State);
            Assert.AreEqual(BattleOutcome.RedWins, battle.Result.Outcome);
        }

        [UnityTest]
        public IEnumerator TiedAtTimeUp_GoesToOvertime_WhereTheNextCrownWins()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;

            battle.DebugSetTimeRemaining(0.05f);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(BattleState.Overtime, battle.State);
            Assert.IsTrue(battle.DoubleElixir);

            FindTower(Team.Red, false, 1).ForceDestroy();
            yield return null;

            Assert.AreEqual(BattleState.Ended, battle.State);
            Assert.AreEqual(BattleOutcome.BlueWins, battle.Result.Outcome);
            Assert.IsTrue(battle.Result.WentToOvertime);
        }

        [UnityTest]
        public IEnumerator TiedAfterOvertime_IsADraw()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;

            battle.DebugSetTimeRemaining(0.05f);
            yield return new WaitForSeconds(0.2f);
            battle.DebugSetTimeRemaining(0.05f);
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(BattleOutcome.Draw, battle.Result.Outcome);
        }

        [UnityTest]
        public IEnumerator Deploy_FollowsTheElixirAndSideRules()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;
            var layout = battle.Layout;
            var bank = battle.Bank(Team.Blue);
            var card = battle.Deck(Team.Blue).Hand(0);

            Vector2 ownSide = layout.Center + new Vector2(-1.5f, -2.5f);
            Vector2 enemySide = layout.Center + new Vector2(-1.5f, 2.5f);

            bank.DebugSet(10f);
            Assert.IsFalse(battle.TryDeploy(Team.Blue, 0, enemySide), "Cannot deploy on the enemy half");

            bank.DebugSet(card.elixirCost - 0.5f);
            Assert.IsFalse(battle.TryDeploy(Team.Blue, 0, ownSide), "Cannot deploy without enough elixir");

            bank.DebugSet(10f);
            int before = BattleRegistry.CountUnits(Team.Blue);
            Assert.IsTrue(battle.TryDeploy(Team.Blue, 0, ownSide));
            Assert.AreEqual(10f - card.elixirCost, bank.Current, 0.01f);
            Assert.AreEqual(before + card.unitCount, BattleRegistry.CountUnits(Team.Blue));
            Assert.AreNotEqual(card, battle.Deck(Team.Blue).Hand(0), "Played card leaves the hand");
        }

        [UnityTest]
        public IEnumerator Pausing_FreezesTheClock_AndBlocksDeploys()
        {
            yield return LoadBattle();
            var battle = BattleManager.Instance;
            battle.Bank(Team.Blue).DebugSet(10f);

            battle.SetPaused(true);
            float time = battle.TimeRemaining;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(time, battle.TimeRemaining, 0.0001f);
            Assert.IsFalse(battle.TryDeploy(Team.Blue, 0, battle.Layout.Center + new Vector2(0f, -2.5f)));

            battle.SetPaused(false);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
