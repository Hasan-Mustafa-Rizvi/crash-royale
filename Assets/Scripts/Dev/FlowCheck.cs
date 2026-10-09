#if UNITY_STANDALONE
using System.Collections;
using System.Linq;
using CrashRoyale.Audio;
using CrashRoyale.Battle;
using CrashRoyale.Core;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using CrashRoyale.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CrashRoyale.Dev
{
    /// <summary>
    /// Plays through the game the way a person would, pressing the real buttons, and checks that every screen hands off
    /// cleanly to the next one. The original build broke exactly here (menu to battle and back).
    /// </summary>
    internal sealed class FlowCheck
    {
        private readonly DevHarness h;
        private CardDatabase db;

        public FlowCheck(DevHarness harness) => h = harness;

        public IEnumerator Run()
        {
            h.Report("== Flow ==");
            db = CardDatabase.Load();

            yield return DevUtil.WaitForScene(SceneFlow.MainMenu);
            h.Check(DevUtil.ActiveScene == SceneFlow.MainMenu, "the game starts on the main menu");
            CheckClean("main menu");

            yield return FirstLaunch();
            yield return ShopAndDeck();
            yield return DeckReachesBattle();
            yield return PauseAndQuit();
            yield return RestartFromPause();
            yield return WinThenMenu();
            yield return LoseThenPlayAgain();
            yield return CrownRules();
        }

        private IEnumerator FirstLaunch()
        {
            h.Check(DevUtil.IsActive("NamePrompt"), "first launch asks for a name");
            var input = DevUtil.Find<TMP_InputField>();
            if (!h.Check(input != null, "the name field exists")) yield break;

            input.text = "x";
            DevUtil.Click("Confirm");
            yield return null;
            h.Check(DevUtil.IsActive("NamePrompt") && !PlayerProfile.HasName, "a one-letter name is rejected");

            input.text = "Flow Tester!";
            DevUtil.Click("Confirm");
            yield return null;
            h.Check(PlayerProfile.Name == "Flow Tester", $"the name is cleaned up and saved (got '{PlayerProfile.Name}')");
            h.Check(!DevUtil.IsActive("NamePrompt"), "the name prompt closes");

            h.Check(DevUtil.Click("DifficultyHARD") && PlayerProfile.Difficulty == Difficulty.Hard, "difficulty pill saves Hard");
            h.Check(DevUtil.Click("DifficultyNORMAL") && PlayerProfile.Difficulty == Difficulty.Normal, "difficulty pill saves Normal");
        }

        private IEnumerator ShopAndDeck()
        {
            var archers = db.Get("archers");
            h.Check(archers != null && !PlayerProfile.IsUnlocked(archers), "Archers start locked");
            PlayerProfile.AddGold(1000);

            h.Check(DevUtil.Click("ShopButton"), "menu: shop button");
            yield return DevUtil.WaitForScene(SceneFlow.Shop);
            h.Check(DevUtil.ActiveScene == SceneFlow.Shop, "the shop opens");
            CheckClean("shop");

            var items = Object.FindObjectsByType<ShopItemView>(FindObjectsSortMode.None);
            var cardItem = items.FirstOrDefault(v => v.Card == archers);
            if (h.Check(cardItem != null, "the shop sells Archers"))
            {
                int gold = PlayerProfile.Gold;
                cardItem.BuyButton.onClick.Invoke();
                yield return null;
                h.Check(PlayerProfile.IsUnlocked(archers), "buying Archers unlocks them");
                h.Check(PlayerProfile.Gold == gold - archers.unlockPrice, $"Archers cost {archers.unlockPrice} gold");
                h.Check(!cardItem.BuyButton.interactable, "an owned card can't be bought twice");
            }

            var emoteItem = items.FirstOrDefault(v => v.Emote != null);
            if (h.Check(emoteItem != null, "the shop sells emotes"))
            {
                emoteItem.BuyButton.onClick.Invoke();
                yield return null;
                h.Check(PlayerProfile.OwnsEmote(emoteItem.Emote), $"buying the {emoteItem.Emote.id} emote grants it");
            }

            h.Check(DevUtil.Click("DeckButton"), "shop: edit deck button");
            yield return DevUtil.WaitForScene(SceneFlow.Deck);
            h.Check(DevUtil.ActiveScene == SceneFlow.Deck, "the deck screen opens");
            CheckClean("deck");

            // Tap Archers in the collection, then tap the first deck slot.
            var views = Object.FindObjectsByType<CardView>(FindObjectsSortMode.None);
            var inCollection = views.FirstOrDefault(v => v.Card == archers && v.name.StartsWith("CardTemplate"));
            var slot = views.FirstOrDefault(v => v.name == "Slot0");
            if (h.Check(inCollection != null && slot != null, "the deck screen shows the collection and the deck slots"))
            {
                string replaced = PlayerProfile.Deck[0];
                inCollection.GetComponent<Button>().onClick.Invoke();
                slot.GetComponent<Button>().onClick.Invoke();
                yield return null;
                h.Check(PlayerProfile.Deck.Contains("archers") && !PlayerProfile.Deck.Contains(replaced), $"Archers replace {replaced} in the saved deck");
            }

            h.Check(DevUtil.Click("Back"), "deck: back button");
            yield return DevUtil.WaitForScene(SceneFlow.MainMenu);
            CheckClean("menu after the deck screen");
        }

        private IEnumerator DeckReachesBattle()
        {
            var saved = PlayerProfile.Deck.ToList();
            h.Check(DevUtil.Click("BattleButton"), "menu: battle button");
            yield return DevUtil.WaitForScene(SceneFlow.Battle);
            var battle = BattleManager.Instance;
            if (!h.Check(battle != null, "the battle loads")) yield break;
            CheckClean("battle", true);

            var deck = battle.Deck(Team.Blue).Cards.Select(c => c.id).ToList();
            h.Check(deck.Count == saved.Count && saved.All(deck.Contains), $"the battle uses the saved deck ({string.Join(", ", deck)})");
            h.Check(battle.Deck(Team.Red).Cards.Count == PlayerProfile.DeckSize, "the AI has a full deck");
            h.Check(battle.RedDifficulty == PlayerProfile.Difficulty, "the AI plays at the chosen difficulty");
            h.Check(DevUtil.AliveTowers(Team.Blue) == 3 && DevUtil.AliveTowers(Team.Red) == 3, "six towers are up");

            yield return DevUtil.WaitFor(() => battle.IsRunning, 6f);
            h.Check(battle.IsRunning, "the intro hands over to the match");

            var bank = battle.Bank(Team.Blue);
            var layout = battle.Layout;
            Vector2 ours = layout.TowerSpot(Team.Blue, false, 0) + new Vector2(0.3f, 1.4f);
            Vector2 theirs = layout.TowerSpot(Team.Red, false, 0) + new Vector2(0.3f, -1.4f);
            bank.DebugSet(10f);
            h.Check(!battle.TryDeploy(Team.Blue, 0, theirs), "deploying on the enemy side is refused");

            var card = battle.Deck(Team.Blue).Hand(0);
            int before = BattleRegistry.CountUnits(Team.Blue);
            h.Check(battle.TryDeploy(Team.Blue, 0, ours), $"{card.displayName} deploys on our side");
            h.Check(Mathf.Abs(bank.Current - (10f - card.elixirCost)) < 0.01f, $"it costs {card.elixirCost} elixir");
            h.Check(BattleRegistry.CountUnits(Team.Blue) == before + card.unitCount, $"{card.unitCount} troop(s) spawn");
            h.Check(battle.Deck(Team.Blue).Hand(0) != card, "the played card cycles out of the hand");

            bank.DebugSet(0f);
            var next = battle.Deck(Team.Blue).Hand(0);
            h.Check(!battle.TryDeploy(Team.Blue, 0, ours), "no elixir, no deploy");
            h.Check(battle.Deck(Team.Blue).Hand(0) == next, "a refused deploy keeps the card");

            // The AI plays by the same rules, so it has to wait for elixir too.
            int redPlays = 0;
            battle.CardDeployed += (team, c, p) =>
            {
                if (team == Team.Red) redPlays++;
            };
            Time.timeScale = 4f;
            yield return DevUtil.WaitFor(() => redPlays > 0, 12f);
            Time.timeScale = 1f;
            h.Check(redPlays > 0, "the red AI deploys troops");
            float redElixir = battle.Bank(Team.Red).Current;
            h.Check(redElixir >= 0f && redElixir <= ElixirBank.Max, $"red elixir stays between 0 and 10 ({redElixir:0.0})");

            int emotes = 0;
            battle.EmoteShown += (team, emote) => emotes++;
            battle.ShowEmote(Team.Blue, "gg");
            battle.ShowEmote(Team.Blue, "gg");
            h.Check(emotes == 1, "emotes work and have a cooldown");
        }

        private IEnumerator PauseAndQuit()
        {
            var battle = BattleManager.Instance;
            if (battle == null) yield break;

            h.Check(DevUtil.Click("PauseButton") && battle.Paused && Time.timeScale == 0f, "the pause button freezes the match");
            battle.Bank(Team.Blue).DebugSet(10f);
            var spot = battle.Layout.TowerSpot(Team.Blue, false, 1) + new Vector2(0f, 1.4f);
            h.Check(!battle.TryDeploy(Team.Blue, 0, spot), "no deploying while paused");
            h.Check(DevUtil.Click("Resume") && !battle.Paused && Time.timeScale == 1f, "resume unfreezes");

            DevUtil.Click("PauseButton");
            h.Check(DevUtil.Click("Quit"), "pause: main menu button");
            yield return DevUtil.WaitForScene(SceneFlow.MainMenu);
            h.Check(DevUtil.ActiveScene == SceneFlow.MainMenu, "quitting a paused battle reaches the menu");
            CheckClean("menu after quitting a paused battle");
            h.Check(PlayerProfile.Wins + PlayerProfile.Losses + PlayerProfile.Draws == 0, "quitting doesn't count as a result");
        }

        private IEnumerator RestartFromPause()
        {
            h.Check(DevUtil.Click("BattleButton"), "menu: battle button again");
            yield return DevUtil.WaitForScene(SceneFlow.Battle);
            var first = BattleManager.Instance;
            yield return DevUtil.WaitFor(() => first != null && first.IsRunning, 6f);
            if (first == null) yield break;

            first.Bank(Team.Blue).DebugSet(10f);
            first.TryDeploy(Team.Blue, 0, first.Layout.TowerSpot(Team.Blue, false, 1) + new Vector2(0f, 1.4f));

            DevUtil.Click("PauseButton");
            h.Check(DevUtil.Click("Restart"), "pause: restart button");
            yield return DevUtil.WaitForNewBattle(first);
            var second = BattleManager.Instance;
            h.Check(DevUtil.IsNewBattle(first), "restart loads a fresh battle");
            h.Check(Time.timeScale == 1f, "restart unpauses");
            h.Check(DevUtil.AliveTowers(Team.Blue) == 3 && DevUtil.AliveTowers(Team.Red) == 3, "the new battle has six towers");
            h.Check(BattleRegistry.CountUnits(Team.Blue) == 0, "no troops leak into the new battle");
            h.Check(second != null && second.Crowns(Team.Blue) == 0 && second.TimeRemaining > 100f, "the clock and crowns reset");
        }

        private IEnumerator WinThenMenu()
        {
            var battle = BattleManager.Instance;
            if (battle == null) yield break;
            yield return DevUtil.WaitFor(() => battle.IsRunning, 6f);

            int wins = PlayerProfile.Wins, gold = PlayerProfile.Gold, trophies = PlayerProfile.Trophies;
            DevUtil.FindTower(battle, Team.Red, true).ForceDestroy();
            yield return null;

            var result = battle.Result;
            h.Check(battle.State == BattleState.Ended && result.Outcome == BattleOutcome.BlueWins && result.BlueCrowns == 3 && result.KingDestroyed,
                "destroying the red king wins with three crowns");
            yield return DevUtil.WaitFor(() => DevUtil.AliveTowers(Team.Red) == 0, 4f);
            h.Check(DevUtil.AliveTowers(Team.Red) == 0, "the loser's other towers crumble");
            h.Check(PlayerProfile.Wins == wins + 1, "the win is recorded");
            h.Check(PlayerProfile.Gold > gold && PlayerProfile.Trophies > trophies,
                $"a win pays gold (+{PlayerProfile.Gold - gold}) and trophies (+{PlayerProfile.Trophies - trophies})");

            yield return DevUtil.WaitFor(() => DevUtil.FindButton("MainMenu") != null, 6f);
            h.Check(DevUtil.Click("MainMenu"), "result screen: main menu button");
            yield return DevUtil.WaitForScene(SceneFlow.MainMenu);
            CheckClean("menu after a win");
        }

        private IEnumerator LoseThenPlayAgain()
        {
            DevUtil.Click("BattleButton");
            yield return DevUtil.WaitForScene(SceneFlow.Battle);
            var battle = BattleManager.Instance;
            if (battle == null) yield break;
            yield return DevUtil.WaitFor(() => battle.IsRunning, 6f);

            int losses = PlayerProfile.Losses;
            DevUtil.FindTower(battle, Team.Blue, true).ForceDestroy();
            yield return null;
            h.Check(battle.Result.Outcome == BattleOutcome.RedWins && battle.Result.RedCrowns == 3, "losing our king is a defeat");
            h.Check(PlayerProfile.Losses == losses + 1, "the loss is recorded");

            yield return DevUtil.WaitFor(() => DevUtil.FindButton("PlayAgain") != null, 6f);
            h.Check(DevUtil.Click("PlayAgain"), "result screen: play again button");
            yield return DevUtil.WaitForNewBattle(battle);
            var next = BattleManager.Instance;
            h.Check(DevUtil.IsNewBattle(battle) && next.State != BattleState.Ended, "play again starts a new battle");
            h.Check(DevUtil.AliveTowers(Team.Blue) == 3 && DevUtil.AliveTowers(Team.Red) == 3, "with six fresh towers");
        }

        private IEnumerator CrownRules()
        {
            var battle = BattleManager.Instance;
            if (battle == null) yield break;
            yield return DevUtil.WaitFor(() => battle.IsRunning, 6f);

            var king = DevUtil.FindTower(battle, Team.Red, true);
            h.Check(!king.Active, "the red king starts asleep");
            DevUtil.FindTower(battle, Team.Red, false, 0).ForceDestroy();
            yield return null;
            h.Check(battle.Crowns(Team.Blue) == 1, "a princess tower is worth one crown");
            h.Check(king.Active, "losing a princess tower wakes the king");
            h.Check(battle.Layout.PocketOpen(Team.Blue, 0), "and opens a deploy pocket in that lane");
            battle.DebugSetTimeRemaining(0.05f);
            yield return DevUtil.WaitFor(() => battle.State == BattleState.Ended, 3f);
            h.Check(battle.Result.Outcome == BattleOutcome.BlueWins && !battle.Result.KingDestroyed, "at time up, more crowns wins");

            yield return LoadDirect();
            battle = BattleManager.Instance;
            battle.DebugSetTimeRemaining(0.05f);
            yield return DevUtil.WaitFor(() => battle.State == BattleState.Overtime, 3f);
            h.Check(battle.State == BattleState.Overtime && battle.DoubleElixir, "a tie at time up goes to overtime with 2x elixir");
            DevUtil.FindTower(battle, Team.Blue, false, 1).ForceDestroy();
            yield return null;
            h.Check(battle.State == BattleState.Ended && battle.Result.Outcome == BattleOutcome.RedWins && battle.Result.WentToOvertime,
                "the first crown in overtime wins");

            yield return LoadDirect();
            battle = BattleManager.Instance;
            battle.DebugSetTimeRemaining(0.05f);
            yield return DevUtil.WaitFor(() => battle.State == BattleState.Overtime, 3f);
            battle.DebugSetTimeRemaining(0.05f);
            yield return DevUtil.WaitFor(() => battle.State == BattleState.Ended, 3f);
            h.Check(battle.Result.Outcome == BattleOutcome.Draw, "still tied after overtime is a draw");

            SceneFlow.Load(SceneFlow.MainMenu);
            yield return DevUtil.WaitForScene(SceneFlow.MainMenu);
            CheckClean("menu at the end");
        }

        private static IEnumerator LoadDirect()
        {
            var previous = BattleManager.Instance;
            BattleSettings.ResetDebugOptions();
            BattleSettings.RedIsAI = false;
            BattleSettings.SkipIntro = true;
            BattleSettings.ApplyRewards = false;
            SceneFlow.Load(SceneFlow.Battle);
            yield return DevUtil.WaitForNewBattle(previous);
            yield return DevUtil.WaitFor(() => BattleManager.Instance != null && BattleManager.Instance.IsRunning, 5f);
        }

        private void CheckClean(string where, bool inBattle = false)
        {
            h.Check(DevUtil.Count<AudioManager>() == 1, $"{where}: one AudioManager");
            h.Check(DevUtil.Count<EventSystem>() == 1, $"{where}: one EventSystem");
            h.Check(DevUtil.Count<SceneFader>() == 1, $"{where}: one SceneFader");
            h.Check(Mathf.Approximately(Time.timeScale, 1f), $"{where}: time runs at normal speed");
            if (!inBattle) h.Check(BattleRegistry.All.Count == 0, $"{where}: nothing left over from a battle");
        }
    }
}
#endif
