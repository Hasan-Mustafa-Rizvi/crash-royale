using System;
using System.Collections;
using System.Collections.Generic;
using CrashRoyale.Audio;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using UnityEngine;

namespace CrashRoyale.Battle
{
    public enum BattleState
    {
        Intro,
        Playing,
        Overtime,
        Ended
    }

    public enum BattleOutcome
    {
        None,
        BlueWins,
        RedWins,
        Draw
    }

    public struct BattleResult
    {
        public BattleOutcome Outcome;
        public int BlueCrowns;
        public int RedCrowns;
        public float Duration;
        public bool KingDestroyed;
        public bool WentToOvertime;
    }

    /// <summary>
    /// Runs a match: the clock, double elixir, overtime, crowns and the result. Both sides deploy through
    /// <see cref="TryDeploy"/>, so the player and the AI play by exactly the same rules.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [SerializeField] private ArenaLayout layout;
        [SerializeField] private BattleAssets assets;
        [SerializeField] private Tower[] towers = Array.Empty<Tower>();
        [SerializeField] private AIController redAI;
        [SerializeField] private AIController blueAI;

        [Header("Rules")]
        [SerializeField] private float regularTime = 120f;
        [SerializeField] private float overtimeTime = 60f;
        [SerializeField] private float doubleElixirFrom = 60f;
        [SerializeField] private float introTime = 2.4f;
        [SerializeField] private float startElixir = 5f;
        [SerializeField] private float elixirPerSecond = 0.5f;
        [SerializeField] private int handSize = 3;
        [SerializeField] private float emoteCooldown = 2.5f;

        private readonly ElixirBank[] banks = new ElixirBank[2];
        private readonly DeckCycle[] decks = new DeckCycle[2];
        private readonly int[] crowns = new int[2];
        private readonly float[] nextEmoteTime = new float[2];
        private readonly int[] unitsLost = new int[2];
        private System.Random rng;
        private float introTimer;
        private bool doubleElixirAnnounced;
        private bool wentToOvertime;

        public static BattleManager Instance { get; private set; }

        public event Action<BattleState> StateChanged;
        public event Action<Team, int> CrownsChanged;
        public event Action DoubleElixirStarted;
        public event Action<BattleResult> BattleEnded;
        public event Action<Team, EmoteData> EmoteShown;
        public event Action<Team, CardData, Vector2> CardDeployed;
        public event Action<Tower> TowerDestroyed;

        public BattleState State { get; private set; } = BattleState.Intro;
        public bool IsRunning => State == BattleState.Playing || State == BattleState.Overtime;
        public bool IsOvertime => State == BattleState.Overtime;
        public float TimeRemaining { get; private set; }
        public float Elapsed { get; private set; }
        public float IntroTime => introTime;
        public bool DoubleElixir => State == BattleState.Overtime || (State == BattleState.Playing && TimeRemaining <= doubleElixirFrom);
        public float ElixirMultiplier => DoubleElixir ? 2f : 1f;
        public bool Paused { get; private set; }
        public BattleResult Result { get; private set; }
        public Reward LastReward { get; private set; }
        public bool RewardApplied { get; private set; }
        public ArenaLayout Layout => layout;
        public BattleAssets Assets => assets;
        public IReadOnlyList<Tower> Towers => towers;
        public Difficulty RedDifficulty { get; private set; }

        public ElixirBank Bank(Team team) => banks[(int)team];
        public DeckCycle Deck(Team team) => decks[(int)team];
        public int Crowns(Team team) => crowns[(int)team];
        public int UnitsLost(Team team) => unitsLost[(int)team];
        public AIController AI(Team team) => team == Team.Red ? redAI : blueAI;

        private void Awake()
        {
            Instance = this;
            Time.timeScale = BattleSettings.TimeScale;

            // Everything the HUD reads in its own Start() is set up here, because Start() order between objects is
            // not guaranteed.
            rng = BattleSettings.Seed != 0 ? new System.Random(BattleSettings.Seed) : new System.Random();
            RedDifficulty = BattleSettings.Difficulty;

            decks[(int)Team.Blue] = new DeckCycle(ResolveDeck(Team.Blue), handSize, rng);
            decks[(int)Team.Red] = new DeckCycle(ResolveDeck(Team.Red), handSize, rng);
            banks[(int)Team.Blue] = new ElixirBank(startElixir, elixirPerSecond);
            banks[(int)Team.Red] = new ElixirBank(startElixir, elixirPerSecond);

            TimeRemaining = regularTime;
            introTimer = BattleSettings.SkipIntro ? 0f : introTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Troops and towers unregister themselves when disabled. Clearing here could also wipe the towers of the
            // next battle when Battle reloads itself (Restart, Play Again), depending on load order.
            BattleRegistry.Prune();
            Time.timeScale = 1f;
        }

        private void Start()
        {
            foreach (var tower in towers)
            {
                if (tower != null) tower.Init();
            }

            if (redAI != null)
            {
                if (BattleSettings.RedIsAI) redAI.Begin(Team.Red, RedDifficulty, this, rng.Next());
                else redAI.enabled = false;
            }

            if (blueAI != null)
            {
                if (BattleSettings.BlueIsAI) blueAI.Begin(Team.Blue, BattleSettings.BlueDifficulty, this, rng.Next());
                else blueAI.enabled = false;
            }

            AudioManager.PlayMusic(MusicTrack.Battle);
            SetState(BattleState.Intro);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (State)
            {
                case BattleState.Intro:
                    introTimer -= dt;
                    if (introTimer <= 0f)
                    {
                        AudioManager.Play(Sfx.BattleStart);
                        SetState(BattleState.Playing);
                    }

                    break;

                case BattleState.Playing:
                case BattleState.Overtime:
                    Elapsed += dt;
                    TimeRemaining -= dt;

                    float multiplier = ElixirMultiplier;
                    banks[0].Tick(dt, multiplier);
                    banks[1].Tick(dt, multiplier);

                    if (DoubleElixir && !doubleElixirAnnounced)
                    {
                        doubleElixirAnnounced = true;
                        DoubleElixirStarted?.Invoke();
                    }

                    if (TimeRemaining <= 0f) OnTimeUp();
                    break;
            }
        }

        public bool CanDeploy(Team team, CardData card, Vector2 position)
        {
            return IsRunning && card != null && layout.IsDeployable(team, position) && Bank(team).CanAfford(card.elixirCost);
        }

        public bool TryDeploy(Team team, int handIndex, Vector2 position)
        {
            if (!IsRunning || Paused) return false;

            var deck = Deck(team);
            var card = deck.Hand(handIndex);
            if (card == null || !layout.IsDeployable(team, position)) return false;
            if (!Bank(team).TrySpend(card.elixirCost)) return false;

            deck.Play(handIndex);
            Deployer.Spawn(assets, card, team, position);
            CardDeployed?.Invoke(team, card, position);
            return true;
        }

        public void ShowEmote(Team team, string emoteId)
        {
            if (State == BattleState.Intro || assets == null || assets.database == null) return;
            var emote = assets.database.GetEmote(emoteId);
            if (emote == null) return;

            int index = (int)team;
            if (Time.unscaledTime < nextEmoteTime[index]) return;
            nextEmoteTime[index] = Time.unscaledTime + emoteCooldown;

            EmoteShown?.Invoke(team, emote);
            AudioManager.Play(Sfx.Emote);
        }

        public void SetPaused(bool paused)
        {
            if (paused && State == BattleState.Ended) return;
            Paused = paused;
            Time.timeScale = paused ? 0f : BattleSettings.TimeScale;
        }

        public string DisplayName(Team team)
        {
            if (team == Team.Blue)
            {
                if (BattleSettings.BlueIsAI) return BattleSettings.BlueNameOverride ?? $"Bot ({BattleSettings.BlueDifficulty})";
                return PlayerProfile.HasName ? PlayerProfile.Name : "You";
            }

            return $"AI ({RedDifficulty})";
        }

        internal void DebugSetTimeRemaining(float seconds) => TimeRemaining = seconds;

        internal void DebugSkipIntro()
        {
            if (State == BattleState.Intro) introTimer = 0f;
        }

        internal void NotifyUnitDied(Unit unit)
        {
            if (unit != null) unitsLost[(int)unit.Team]++;
        }

        internal void NotifyTowerDestroyed(Tower tower)
        {
            TowerDestroyed?.Invoke(tower);
            if (State == BattleState.Ended) return;

            Team attacker = tower.Team.Opponent();
            int a = (int)attacker;

            if (tower.IsKing)
            {
                crowns[a] = 3;
                CrownsChanged?.Invoke(attacker, crowns[a]);
                End(attacker == Team.Blue ? BattleOutcome.BlueWins : BattleOutcome.RedWins, true);
                StartCoroutine(CrumbleRemaining(tower.Team));
                return;
            }

            crowns[a] = Mathf.Min(3, crowns[a] + 1);
            CrownsChanged?.Invoke(attacker, crowns[a]);

            foreach (var t in towers)
            {
                if (t != null && t.Team == tower.Team && t.IsKing) t.Activate();
            }

            if (State == BattleState.Overtime) End(attacker == Team.Blue ? BattleOutcome.BlueWins : BattleOutcome.RedWins, false);
        }

        private IEnumerator CrumbleRemaining(Team loser)
        {
            yield return new WaitForSeconds(0.4f);
            foreach (var t in towers)
            {
                if (t == null || t.Team != loser || !t.IsAlive) continue;
                t.ForceDestroy();
                yield return new WaitForSeconds(0.25f);
            }
        }

        private void OnTimeUp()
        {
            TimeRemaining = 0f;
            int blue = crowns[(int)Team.Blue];
            int red = crowns[(int)Team.Red];

            if (blue != red)
            {
                End(blue > red ? BattleOutcome.BlueWins : BattleOutcome.RedWins, false);
            }
            else if (State == BattleState.Playing && overtimeTime > 0f)
            {
                wentToOvertime = true;
                TimeRemaining = overtimeTime;
                AudioManager.Play(Sfx.Overtime);
                SetState(BattleState.Overtime);
            }
            else
            {
                End(BattleOutcome.Draw, false);
            }
        }

        private void End(BattleOutcome outcome, bool kingDestroyed)
        {
            if (State == BattleState.Ended) return;

            Result = new BattleResult
            {
                Outcome = outcome,
                BlueCrowns = crowns[(int)Team.Blue],
                RedCrowns = crowns[(int)Team.Red],
                Duration = Elapsed,
                KingDestroyed = kingDestroyed,
                WentToOvertime = wentToOvertime
            };

            SetState(BattleState.Ended);

            if (redAI != null) redAI.Stop();
            if (blueAI != null) blueAI.Stop();

            if (BattleSettings.ApplyRewards && !BattleSettings.BlueIsAI)
            {
                LastReward = PlayerProfile.ApplyBattleResult(Result, RedDifficulty);
                RewardApplied = true;
            }

            AudioManager.StopMusic();
            AudioManager.Play(outcome == BattleOutcome.BlueWins ? Sfx.Victory : outcome == BattleOutcome.RedWins ? Sfx.Defeat : Sfx.Draw);
            BattleEnded?.Invoke(Result);
        }

        private void SetState(BattleState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private List<CardData> ResolveDeck(Team team)
        {
            var db = assets.database;
            string[] ids;

            if (team == Team.Blue)
                ids = BattleSettings.BlueDeckOverride ?? (BattleSettings.BlueIsAI ? PickAiDeck(BattleSettings.BlueDifficulty) : ToArray(PlayerProfile.Deck));
            else
                ids = BattleSettings.RedDeckOverride ?? PickAiDeck(RedDifficulty);

            var cards = new List<CardData>();
            foreach (var id in ids)
            {
                var card = db.Get(id);
                if (card != null && !cards.Contains(card)) cards.Add(card);
            }

            // Top up with defaults so a broken save can never start a battle with an empty hand.
            foreach (var id in db.defaultDeck)
            {
                if (cards.Count >= PlayerProfile.DeckSize) break;
                var card = db.Get(id);
                if (card != null && !cards.Contains(card)) cards.Add(card);
            }

            return cards;
        }

        private string[] PickAiDeck(Difficulty difficulty)
        {
            var db = assets.database;
            if (difficulty != Difficulty.Easy && db.aiDecks.Count > 0)
            {
                var preset = db.aiDecks[rng.Next(db.aiDecks.Count)];
                return preset.cardIds.ToArray();
            }

            var pool = new List<string>();
            foreach (var card in db.cards)
            {
                if (card != null) pool.Add(card.id);
            }

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return pool.GetRange(0, Mathf.Min(PlayerProfile.DeckSize, pool.Count)).ToArray();
        }

        private static string[] ToArray(IReadOnlyList<string> list)
        {
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = list[i];
            return result;
        }
    }
}
