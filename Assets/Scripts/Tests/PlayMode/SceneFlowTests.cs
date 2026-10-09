using System.Collections;
using CrashRoyale.Audio;
using CrashRoyale.Battle;
using CrashRoyale.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CrashRoyale.Tests
{
    /// <summary>The old build broke going between the menu and the battle. These walk that path repeatedly.</summary>
    public class SceneFlowTests : BattleTestBase
    {
        private static IEnumerator WaitForScene(string name, float seconds = 10f)
        {
            float timeout = Time.realtimeSinceStartup + seconds;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        [UnityTest]
        public IEnumerator MenuToBattleAndBack_KeepsOneAudioManager_AndResetsTimeScale()
        {
            BattleSettings.RedIsAI = false;
            SceneManager.LoadScene(SceneFlow.MainMenu);
            yield return WaitForScene(SceneFlow.MainMenu);

            for (int round = 0; round < 2; round++)
            {
                SceneFlow.Load(SceneFlow.Battle);
                yield return WaitForScene(SceneFlow.Battle);
                Assert.IsNotNull(BattleManager.Instance);

                BattleManager.Instance.SetPaused(true);
                Assert.AreEqual(0f, Time.timeScale);

                SceneFlow.Load(SceneFlow.MainMenu);
                yield return WaitForScene(SceneFlow.MainMenu);
                Assert.AreEqual(1f, Time.timeScale, "Leaving a paused battle must unpause");
                Assert.AreEqual(1, Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length);
            }
        }

        [UnityTest]
        public IEnumerator EveryScene_LoadsWithoutErrors()
        {
            foreach (var scene in new[] { SceneFlow.MainMenu, SceneFlow.Deck, SceneFlow.Shop, SceneFlow.Battle })
            {
                SceneManager.LoadScene(scene);
                yield return WaitForScene(scene);
            }

            LogAssert.NoUnexpectedReceived();
        }
    }
}
