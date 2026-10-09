#if UNITY_STANDALONE
using System.Collections;
using System.IO;
using System.Linq;
using CrashRoyale.Battle;
using CrashRoyale.Core;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using CrashRoyale.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CrashRoyale.Dev
{
    /// <summary>Renders every screen of the game to 1080x1920 PNGs, plus a short gameplay frame sequence for a GIF.</summary>
    internal sealed class ScreenTour
    {
        private readonly DevHarness h;
        private string folder;

        public ScreenTour(DevHarness harness) => h = harness;

        public IEnumerator Run()
        {
            h.Report("== Screenshots ==");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                h.Fail("screenshots need a graphics device (run without -nographics)");
                yield break;
            }

            folder = Path.Combine(h.OutputDir, "shots");
            Directory.CreateDirectory(folder);

            // Menus
            yield return DevUtil.WaitForScene(SceneFlow.MainMenu);
            yield return Shot("01_first_launch");

            var input = DevUtil.Find<TMP_InputField>();
            if (input != null)
            {
                input.text = "Hasan";
                DevUtil.Click("Confirm");
            }

            SeedProfile();
            yield return Load(SceneFlow.MainMenu);
            yield return Shot("02_main_menu");

            DevUtil.Click("SettingsButton");
            yield return Settle();
            yield return Shot("03_settings");
            DevUtil.Click("Close");

            DevUtil.Click("HelpButton");
            yield return Settle();
            yield return Shot("04_how_to_play");
            DevUtil.Click("Close");

            yield return Load(SceneFlow.Deck);
            var lancer = Object.FindObjectsByType<CardView>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v.Card != null && v.Card.id == "lancer" && v.name.StartsWith("CardTemplate"));
            if (lancer != null) lancer.GetComponent<Button>().onClick.Invoke();
            yield return Settle();
            yield return Shot("05_deck");

            yield return Load(SceneFlow.Shop);
            yield return Shot("06_shop");

            // A bot plays blue under the player's name so the battle looks like a real match.
            BattleSettings.ResetDebugOptions();
            BattleSettings.BlueIsAI = true;
            BattleSettings.BlueNameOverride = PlayerProfile.Name;
            BattleSettings.BlueDifficulty = Difficulty.Hard;
            BattleSettings.Difficulty = Difficulty.Normal;
            BattleSettings.Seed = 77;
            BattleSettings.ApplyRewards = false;
            SceneManager.LoadScene(SceneFlow.Battle);
            yield return DevUtil.WaitFor(() => DevUtil.IsNewBattle(null), 10f);
            var battle = BattleManager.Instance;
            if (!h.Check(battle != null, "battle loads for screenshots")) yield break;

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("07_battle_intro");

            yield return DevUtil.WaitFor(() => battle.IsRunning, 8f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("08_battle_start");

            // Wait for a busy moment, then let the callouts (which run on real time) clear before the shot.
            yield return FastForwardUntil(() => battle.State == BattleState.Ended || battle.Elapsed > 50f || (battle.Elapsed > 16f && TroopCount() >= 9), 60f);
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot("09_battle_fight");
            yield return DevUtil.CaptureSequence(Path.Combine(folder, "gif_early"), 120, 1f / 15f, 540, 960);

            // What dragging a card looks like: lifted card, troop preview and the red no-deploy zones.
            var ghost = DevUtil.Find<DeployGhost>();
            var slots = Object.FindObjectsByType<CardSlotUI>(FindObjectsSortMode.None).OrderBy(s => s.transform.position.x).ToArray();
            if (ghost != null && slots.Length > 1 && battle.State != BattleState.Ended)
            {
                var card = battle.Deck(Team.Blue).Hand(1);
                Vector2 spot = battle.Layout.TowerSpot(Team.Blue, false, 0) + new Vector2(0.6f, 2.0f);
                slots[1].SetDragging(true);
                ghost.Show(card, Team.Blue, spot, battle.Layout.IsDeployable(Team.Blue, spot));
                yield return Shot("10_deploy_preview");
                slots[1].SetDragging(false);
                ghost.Hide();
            }

            yield return FastForward(battle, 60.2f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("11_double_elixir");

            yield return FastForwardUntil(() => battle.State == BattleState.Ended || battle.Elapsed > 100f || (battle.Elapsed > 66f && TroopCount() >= 10), 60f);
            yield return new WaitForSecondsRealtime(1.6f);
            yield return DevUtil.CaptureSequence(Path.Combine(folder, "gif_late"), 120, 1f / 15f, 540, 960);

            yield return FastForwardUntil(() => battle.Crowns(Team.Blue) + battle.Crowns(Team.Red) > 0 || battle.State != BattleState.Playing, 90f);
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shot(battle.Crowns(Team.Blue) + battle.Crowns(Team.Red) > 0 ? "12_tower_down" : "12_overtime");

            if (battle.State != BattleState.Ended)
            {
                DevUtil.Click("EmoteButton");
                battle.ShowEmote(Team.Red, "laugh");
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot("13_emotes");
                DevUtil.Click("EmoteButton");
            }

            if (battle.State != BattleState.Ended)
            {
                DevUtil.Click("PauseButton");
                yield return Settle();
                yield return Shot("14_pause");
                DevUtil.Click("Resume");
            }

            // The player's own battles, so the result screens show real rewards.
            yield return PlayerBattle(Team.Red, "15_victory_callout", "16_result_victory");
            yield return PlayerBattle(Team.Blue, null, "17_result_defeat");

            SceneManager.LoadScene(SceneFlow.MainMenu);
            yield return DevUtil.WaitForScene(SceneFlow.MainMenu);
            h.Report($"screenshots saved to {folder}");
        }

        private IEnumerator PlayerBattle(Team loser, string callout, string result)
        {
            var previous = BattleManager.Instance;
            BattleSettings.ResetDebugOptions();
            BattleSettings.Difficulty = Difficulty.Normal;
            BattleSettings.Seed = loser == Team.Red ? 21 : 34;
            BattleSettings.SkipIntro = true;
            SceneManager.LoadScene(SceneFlow.Battle);
            yield return DevUtil.WaitForNewBattle(previous);
            var battle = BattleManager.Instance;
            yield return DevUtil.WaitFor(() => battle.IsRunning, 5f);

            // A few seconds of real play so the arena isn't empty.
            battle.Bank(Team.Blue).DebugSet(10f);
            battle.TryDeploy(Team.Blue, 0, battle.Layout.TowerSpot(Team.Blue, false, 0) + new Vector2(0.4f, 1.6f));
            battle.TryDeploy(Team.Blue, 0, battle.Layout.TowerSpot(Team.Blue, false, 1) + new Vector2(-0.4f, 1.8f));
            yield return FastForward(battle, 14f);

            DevUtil.FindTower(battle, loser, true).ForceDestroy();
            yield return new WaitForSecondsRealtime(0.6f);
            if (callout != null) yield return Shot(callout);

            yield return DevUtil.WaitFor(() => DevUtil.FindButton("PlayAgain") != null, 6f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(result);
        }

        private static void SeedProfile()
        {
            var db = CardDatabase.Load();
            PlayerProfile.EnsureInitialized(db);
            for (int i = 0; i < 6; i++)
            {
                PlayerProfile.ApplyBattleResult(new BattleResult { Outcome = BattleOutcome.BlueWins, BlueCrowns = i % 3 + 1, KingDestroyed = i % 3 == 2 }, Difficulty.Normal);
            }

            PlayerProfile.ApplyBattleResult(new BattleResult { Outcome = BattleOutcome.RedWins, BlueCrowns = 1, RedCrowns = 2 }, Difficulty.Hard);
            PlayerProfile.ApplyBattleResult(new BattleResult { Outcome = BattleOutcome.Draw, BlueCrowns = 1, RedCrowns = 1 }, Difficulty.Normal);
            PlayerProfile.Unlock("archers");
            PlayerProfile.GrantEmote("laugh");
        }

        private IEnumerator Shot(string name)
        {
            yield return DevUtil.Capture(Path.Combine(folder, name + ".png"));
            h.Report("  shot " + name);
        }

        private static int TroopCount() => BattleRegistry.CountUnits(Team.Blue) + BattleRegistry.CountUnits(Team.Red);

        private static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return DevUtil.WaitForScene(scene);
            yield return Settle();
        }

        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(0.6f);
        }

        private static IEnumerator FastForward(BattleManager battle, float elapsed)
        {
            yield return FastForwardUntil(() => battle == null || battle.Elapsed >= elapsed || battle.State == BattleState.Ended, 120f);
        }

        private static IEnumerator FastForwardUntil(System.Func<bool> done, float realTimeout)
        {
            float limit = Time.realtimeSinceStartup + realTimeout;
            Time.captureDeltaTime = 1f / 30f;
            while (!done() && Time.realtimeSinceStartup < limit) yield return null;
            Time.captureDeltaTime = 0f;
            yield return null;
        }
    }
}
#endif
