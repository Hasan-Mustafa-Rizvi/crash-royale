using System.Collections;
using CrashRoyale.Battle;
using CrashRoyale.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrashRoyale.Tests
{
    /// <summary>Loads the real Battle scene with test settings and a throwaway profile.</summary>
    public abstract class BattleTestBase
    {
        private IKeyValueStore previousStore;

        [SetUp]
        public void BaseSetUp()
        {
            previousStore = PlayerProfile.Store;
            PlayerProfile.Store = new MemoryStore();
            PlayerProfile.Name = "Tester";
            BattleSettings.ResetDebugOptions();
        }

        [TearDown]
        public void BaseTearDown()
        {
            BattleSettings.ResetDebugOptions();
            PlayerProfile.Store = previousStore;
            Time.timeScale = 1f;
        }

        protected static IEnumerator LoadBattle()
        {
            SceneManager.LoadScene("Battle");
            yield return null;
            yield return null;

            var battle = BattleManager.Instance;
            Assert.IsNotNull(battle, "Battle scene has no BattleManager");
            battle.DebugSkipIntro();

            float timeout = Time.realtimeSinceStartup + 10f;
            while (battle.State == BattleState.Intro && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreNotEqual(BattleState.Intro, battle.State, "Battle never left the intro");
        }

        protected static Tower FindTower(Team team, bool king, int lane = -1)
        {
            foreach (var t in BattleManager.Instance.Towers)
            {
                if (t.Team == team && t.IsKing == king && (king || t.Lane == lane)) return t;
            }

            return null;
        }
    }
}
