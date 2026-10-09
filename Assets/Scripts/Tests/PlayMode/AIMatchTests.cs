using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrashRoyale.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CrashRoyale.Tests
{
    /// <summary>
    /// Full AI-vs-AI matches at high speed. They prove both sides deploy on the same elixir rules and that
    /// every match reaches a proper result, and they log the outcomes for balance tuning.
    /// </summary>
    public class AIMatchTests : BattleTestBase
    {
        private static readonly List<string> Log = new List<string>();

        [OneTimeTearDown]
        public void WriteLog()
        {
            if (Log.Count == 0) return;
            Directory.CreateDirectory("Logs");
            File.AppendAllText("Logs/ai_matches.csv", string.Join("\n", Log) + "\n");
            Log.Clear();
        }

        private static IEnumerable<object[]> Matchups()
        {
            yield return new object[] { Difficulty.Normal, Difficulty.Normal, 11 };
            yield return new object[] { Difficulty.Normal, Difficulty.Normal, 22 };
            yield return new object[] { Difficulty.Hard, Difficulty.Easy, 33 };
            yield return new object[] { Difficulty.Easy, Difficulty.Hard, 44 };
            yield return new object[] { Difficulty.Hard, Difficulty.Hard, 55 };
            yield return new object[] { Difficulty.Hard, Difficulty.Normal, 66 };
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AiVsAi_PlaysAFullMatchToAResult([ValueSource(nameof(Matchups))] object[] matchup)
        {
            var blue = (Difficulty)matchup[0];
            var red = (Difficulty)matchup[1];
            int seed = (int)matchup[2];

            BattleSettings.BlueIsAI = true;
            BattleSettings.BlueDifficulty = blue;
            BattleSettings.Difficulty = red;
            BattleSettings.Seed = seed;
            BattleSettings.SkipIntro = true;
            BattleSettings.ApplyRewards = false;
            BattleSettings.TimeScale = 6f;

            yield return LoadBattle();
            var battle = BattleManager.Instance;

            int blueDeploys = 0, redDeploys = 0;
            battle.CardDeployed += (team, card, pos) =>
            {
                if (team == Team.Blue) blueDeploys++;
                else redDeploys++;
                Assert.IsTrue(battle.Layout.IsOnSide(team, pos) || battle.Layout.PocketOpen(team, battle.Layout.LaneOf(pos)),
                    $"{team} deployed {card.displayName} on the wrong side at {pos}");
            };

            float timeout = Time.realtimeSinceStartup + 150f;
            while (battle.State != BattleState.Ended && Time.realtimeSinceStartup < timeout) yield return null;

            Assert.AreEqual(BattleState.Ended, battle.State, "Match did not finish in time");
            Assert.AreNotEqual(BattleOutcome.None, battle.Result.Outcome);
            Assert.Greater(blueDeploys, 3, "Blue AI barely played");
            Assert.Greater(redDeploys, 3, "Red AI barely played");

            var r = battle.Result;
            Log.Add($"{blue},{red},{seed},{r.Outcome},{r.BlueCrowns},{r.RedCrowns},{r.Duration:0.0},{r.WentToOvertime},{blueDeploys},{redDeploys},{battle.Bank(Team.Blue).Wasted:0.0},{battle.Bank(Team.Red).Wasted:0.0}");
            Debug.Log($"[AI match] {blue} vs {red} seed {seed}: {r.Outcome} {r.BlueCrowns}-{r.RedCrowns} in {r.Duration:0}s, deploys {blueDeploys}/{redDeploys}");
        }
    }
}
