#if UNITY_STANDALONE
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CrashRoyale.Battle;
using CrashRoyale.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrashRoyale.Dev
{
    /// <summary>
    /// The AI plays itself at every difficulty pairing and every match is logged. Sides swap each game so blue/red never
    /// skews the numbers. Game time advances in fixed steps (<see cref="Time.captureDeltaTime"/>), so a three-minute match
    /// takes a few seconds and the same seed replays the same match.
    /// </summary>
    internal sealed class AiTournament
    {
        private static readonly (Difficulty a, Difficulty b)[] Pairings =
        {
            (Difficulty.Hard, Difficulty.Easy),
            (Difficulty.Normal, Difficulty.Easy),
            (Difficulty.Hard, Difficulty.Normal),
            (Difficulty.Easy, Difficulty.Easy),
            (Difficulty.Normal, Difficulty.Normal),
            (Difficulty.Hard, Difficulty.Hard)
        };

        private const float MaxGameSeconds = 200f;
        private const float MaxRealSeconds = 240f;

        private readonly DevHarness h;
        private readonly int gamesPerPairing;
        private readonly float step;
        private readonly StringBuilder csv = new StringBuilder();
        private readonly Dictionary<string, CardTally> cards = new Dictionary<string, CardTally>();
        private readonly Dictionary<Difficulty, SideTally> sides = new Dictionary<Difficulty, SideTally>();
        private int blueSideWins;
        private int redSideWins;
        private int totalStuck;
        private int totalCrowded;
        private int totalTimeouts;
        private int stuckReports;

        public AiTournament(DevHarness harness, int games, float fixedStep)
        {
            h = harness;
            gamesPerPairing = Mathf.Max(1, games);
            step = Mathf.Clamp(fixedStep, 0.005f, 0.05f);
        }

        private sealed class CardTally
        {
            public int Decks, Wins, Plays;
        }

        private sealed class SideTally
        {
            public int Games, Plays, Lost, Crowns;
            public float Wasted, FirstPlay;
            public readonly Dictionary<string, int> Actions = new Dictionary<string, int>();
        }

        private sealed class Match
        {
            public Difficulty Blue, Red;
            public int Seed;
            public string BlueDeck = "", RedDeck = "";
            public readonly int[] Plays = new int[2];
            public readonly float[] FirstPlay = { -1f, -1f };
            public readonly int[] MaxUnits = new int[2];
            public readonly float[] Wasted = new float[2];
            public readonly int[] Lost = new int[2];
            public readonly float[] TowerHp = new float[2];
            public float FirstCrown = -1f;
            public int Stuck;
            public int Crowded;
            public bool TimedOut;
            public int Errors;
            public float RealSeconds;
            public BattleResult Result;
        }

        public IEnumerator Run()
        {
            h.Report($"== AI tournament: {gamesPerPairing} games per pairing, {step:0.000}s steps ==");
            csv.AppendLine("pairing,game,seed,blue,red,blue_deck,red_deck,outcome,winner,blue_crowns,red_crowns,duration,overtime,king_down,first_crown," +
                           "blue_plays,red_plays,blue_first_play,red_first_play,blue_wasted,red_wasted,blue_lost,red_lost,blue_tower_hp,red_tower_hp," +
                           "blue_max_units,red_max_units,stuck,crowded,timed_out,errors,real_seconds");

            var rows = new List<string>();
            float started = Time.realtimeSinceStartup;

            for (int p = 0; p < Pairings.Length; p++)
            {
                var (a, b) = Pairings[p];
                int aWins = 0, bWins = 0, draws = 0, overtime = 0, kings = 0, timeouts = 0;
                float duration = 0f;

                for (int g = 0; g < gamesPerPairing; g++)
                {
                    bool swap = g % 2 == 1;
                    var match = new Match { Blue = swap ? b : a, Red = swap ? a : b, Seed = 1009 * (p + 1) + 7919 * (g + 1) };
                    yield return Play(match);
                    Record(match, p, g);

                    var outcome = match.Result.Outcome;
                    bool blueWon = outcome == BattleOutcome.BlueWins;
                    bool redWon = outcome == BattleOutcome.RedWins;
                    if (match.TimedOut) timeouts++;
                    else if (swap ? redWon : blueWon) aWins++;
                    else if (swap ? blueWon : redWon) bWins++;
                    else draws++;

                    if (a == b)
                    {
                        if (blueWon) blueSideWins++;
                        if (redWon) redSideWins++;
                    }

                    if (match.Result.WentToOvertime) overtime++;
                    if (match.Result.KingDestroyed) kings++;
                    duration += match.Result.Duration;
                }

                string row = $"{a,-6} vs {b,-6}  {aWins,3} - {bWins,-3} draws {draws,-3} avg {duration / gamesPerPairing,4:0}s  overtime {overtime,-3} 3-crown {kings,-3} timeouts {timeouts}";
                rows.Add(row);
                h.Report(row);
            }

            WriteSummary(rows, Time.realtimeSinceStartup - started);
            h.Check(totalTimeouts == 0, "every AI match reached a result");

            Time.captureDeltaTime = 0f;
            SceneManager.LoadScene(SceneFlow.MainMenu);
            yield return null;
        }

        /// <summary>
        /// Side check: each seed is played twice with the same two decks, once each way round. In a fair game blue and
        /// red win about equally often.
        /// </summary>
        public IEnumerator RunMirror(int seeds, Difficulty difficulty)
        {
            h.Report($"== Mirror test: {seeds} seeds, each played from both sides, {difficulty} vs {difficulty} ==");
            var db = Data.CardDatabase.Load();
            int blueWins = 0, redWins = 0, draws = 0, deckSwept = 0;

            for (int i = 0; i < seeds; i++)
            {
                var rng = new System.Random(9001 + i * 31);
                string[] deckA = RandomDeck(db, rng), deckB = RandomDeck(db, rng);
                int aWins = 0;

                for (int swap = 0; swap < 2; swap++)
                {
                    var m = new Match { Blue = difficulty, Red = difficulty, Seed = 5003 + i * 17 };
                    yield return Play(m, swap == 0 ? deckA : deckB, swap == 0 ? deckB : deckA);
                    Record(m, 99, i * 2 + swap);

                    var outcome = m.Result.Outcome;
                    if (outcome == BattleOutcome.BlueWins) blueWins++;
                    else if (outcome == BattleOutcome.RedWins) redWins++;
                    else draws++;

                    bool aIsBlue = swap == 0;
                    if ((aIsBlue && outcome == BattleOutcome.BlueWins) || (!aIsBlue && outcome == BattleOutcome.RedWins)) aWins++;
                }

                if (aWins == 2) deckSwept++;
            }

            string line = $"Mirror: blue side won {blueWins}, red side won {redWins}, draws {draws}; same deck won both ways in {deckSwept} of {seeds}";
            h.Report(line);
            h.WriteFile("mirror_summary.txt", line + "\n");
            Time.captureDeltaTime = 0f;
            SceneManager.LoadScene(SceneFlow.MainMenu);
            yield return null;
        }

        private static string[] RandomDeck(Data.CardDatabase db, System.Random rng)
        {
            var ids = db.cards.Where(c => c != null).Select(c => c.id).ToList();
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            return ids.Take(Meta.PlayerProfile.DeckSize).ToArray();
        }

        private IEnumerator Play(Match m, string[] blueDeck = null, string[] redDeck = null)
        {
            BattleSettings.ResetDebugOptions();
            BattleSettings.BlueDeckOverride = blueDeck;
            BattleSettings.RedDeckOverride = redDeck;
            BattleSettings.BlueIsAI = true;
            BattleSettings.BlueDifficulty = m.Blue;
            BattleSettings.Difficulty = m.Red;
            BattleSettings.Seed = m.Seed;
            BattleSettings.SkipIntro = true;
            BattleSettings.ApplyRewards = false;

            int errorsBefore = h.ErrorCount;
            float realStart = Time.realtimeSinceStartup;
            var previous = BattleManager.Instance;

            Time.captureDeltaTime = 0f;
            SceneManager.LoadScene(SceneFlow.Battle);
            yield return DevUtil.WaitFor(() => DevUtil.IsNewBattle(previous), 15f);

            var battle = BattleManager.Instance;
            if (!DevUtil.IsNewBattle(previous))
            {
                h.Fail($"seed {m.Seed}: the battle didn't load");
                m.TimedOut = true;
                totalTimeouts++;
                yield break;
            }

            m.BlueDeck = DeckName(battle.Deck(Team.Blue));
            m.RedDeck = DeckName(battle.Deck(Team.Red));
            battle.CardDeployed += (team, card, position) =>
            {
                int i = (int)team;
                m.Plays[i]++;
                if (m.FirstPlay[i] < 0f) m.FirstPlay[i] = battle.Elapsed;
                Tally(card.id).Plays++;

                var ai = battle.AI(team);
                string action = ai != null && !string.IsNullOrEmpty(ai.LastAction) ? ai.LastAction.Split(' ')[0] : "?";
                var side = Side(team == Team.Blue ? m.Blue : m.Red);
                side.Actions[action] = side.Actions.TryGetValue(action, out int n) ? n + 1 : 1;
            };
            battle.TowerDestroyed += tower =>
            {
                if (m.FirstCrown < 0f) m.FirstCrown = battle.Elapsed;
            };

            var stuck = new StuckWatch(this, m.Seed);
            bool elixirReported = false;
            Time.captureDeltaTime = step;

            while (battle != null && battle.State != BattleState.Ended)
            {
                if (battle.Elapsed > MaxGameSeconds || Time.realtimeSinceStartup - realStart > MaxRealSeconds)
                {
                    m.TimedOut = true;
                    totalTimeouts++;
                    h.Fail($"seed {m.Seed}: match stuck at {battle.Elapsed:0}s in {battle.State}");
                    break;
                }

                for (int i = 0; i < 2; i++)
                {
                    var team = (Team)i;
                    m.MaxUnits[i] = Mathf.Max(m.MaxUnits[i], BattleRegistry.CountUnits(team));
                    float elixir = battle.Bank(team).Current;
                    if (!elixirReported && (elixir < -0.001f || elixir > ElixirBank.Max + 0.001f))
                    {
                        elixirReported = true;
                        h.Fail($"seed {m.Seed}: {team} elixir out of range ({elixir})");
                    }
                }

                stuck.Sample(battle.Elapsed);
                yield return null;
            }

            Time.captureDeltaTime = 0f;
            if (battle == null) yield break;

            m.Result = battle.Result;
            m.Stuck = stuck.Count;
            m.Crowded = stuck.Crowded;
            totalStuck += stuck.Count;
            totalCrowded += stuck.Crowded;
            for (int i = 0; i < 2; i++)
            {
                var team = (Team)i;
                m.Wasted[i] = battle.Bank(team).Wasted;
                m.Lost[i] = battle.UnitsLost(team);
                m.TowerHp[i] = TowerHealth(battle, team);
            }

            m.Errors = h.ErrorCount - errorsBefore;
            m.RealSeconds = Time.realtimeSinceStartup - realStart;
        }

        private void Record(Match m, int pairing, int game)
        {
            var r = m.Result;
            string outcome = m.TimedOut ? "Timeout" : r.Outcome.ToString();
            string winner = r.Outcome == BattleOutcome.BlueWins ? m.Blue.ToString() : r.Outcome == BattleOutcome.RedWins ? m.Red.ToString() : "-";

            csv.AppendLine(string.Join(",",
                pairing, game, m.Seed, m.Blue, m.Red, m.BlueDeck, m.RedDeck, outcome, winner, r.BlueCrowns, r.RedCrowns, F(r.Duration),
                r.WentToOvertime ? 1 : 0, r.KingDestroyed ? 1 : 0, F(m.FirstCrown),
                m.Plays[0], m.Plays[1], F(m.FirstPlay[0]), F(m.FirstPlay[1]), F(m.Wasted[0]), F(m.Wasted[1]), m.Lost[0], m.Lost[1],
                F(m.TowerHp[0], "0.00"), F(m.TowerHp[1], "0.00"), m.MaxUnits[0], m.MaxUnits[1], m.Stuck, m.Crowded, m.TimedOut ? 1 : 0, m.Errors, F(m.RealSeconds)));
            h.WriteFile("ai_matches.csv", csv.ToString());

            if (m.TimedOut) return;

            for (int i = 0; i < 2; i++)
            {
                var team = (Team)i;
                var difficulty = team == Team.Blue ? m.Blue : m.Red;
                bool won = (team == Team.Blue && r.Outcome == BattleOutcome.BlueWins) || (team == Team.Red && r.Outcome == BattleOutcome.RedWins);

                var side = Side(difficulty);
                side.Games++;
                side.Plays += m.Plays[i];
                side.Wasted += m.Wasted[i];
                side.Lost += m.Lost[i];
                side.Crowns += team == Team.Blue ? r.BlueCrowns : r.RedCrowns;
                side.FirstPlay += Mathf.Max(0f, m.FirstPlay[i]);

                foreach (var id in (team == Team.Blue ? m.BlueDeck : m.RedDeck).Split('+'))
                {
                    var tally = Tally(id);
                    tally.Decks++;
                    if (won) tally.Wins++;
                }
            }

            h.Report($"  {m.Blue,-6} (blue) vs {m.Red,-6} (red)  seed {m.Seed,-6} {outcome,-9} {r.BlueCrowns}-{r.RedCrowns} " +
                     $"{r.Duration,4:0}s  plays {m.Plays[0]}/{m.Plays[1]}  wasted {m.Wasted[0]:0}/{m.Wasted[1]:0}  stuck {m.Stuck}/{m.Crowded}  real {m.RealSeconds:0.0}s");
        }

        private void WriteSummary(List<string> rows, float realSeconds)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"AI tournament, {gamesPerPairing} games per pairing ({Pairings.Length * gamesPerPairing} total), {realSeconds:0}s real time");
            sb.AppendLine();
            sb.AppendLine("Pairing (A vs B)  A wins - B wins");
            foreach (var row in rows) sb.AppendLine(row);
            sb.AppendLine();
            sb.AppendLine($"Mirror matches by side: blue {blueSideWins}, red {redSideWins}");
            sb.AppendLine();
            sb.AppendLine("Per difficulty (averages per game)");
            foreach (var pair in sides.OrderBy(s => s.Key))
            {
                var s = pair.Value;
                float n = Mathf.Max(1, s.Games);
                sb.AppendLine($"  {pair.Key,-6}  plays {s.Plays / n,5:0.0}  wasted elixir {s.Wasted / n,5:0.0}  troops lost {s.Lost / n,5:0.0}  crowns {s.Crowns / n:0.00}  first play at {s.FirstPlay / n:0.0}s");
                sb.AppendLine("          " + string.Join("  ", s.Actions.OrderByDescending(a => a.Value).Select(a => $"{a.Key} {a.Value / n:0.0}")));
            }

            sb.AppendLine();
            sb.AppendLine("Per card (win rate of decks that had it)");
            foreach (var pair in cards.OrderByDescending(c => c.Value.Decks > 0 ? (float)c.Value.Wins / c.Value.Decks : 0f))
            {
                var c = pair.Value;
                float rate = c.Decks > 0 ? 100f * c.Wins / c.Decks : 0f;
                sb.AppendLine($"  {pair.Key,-10} in {c.Decks,4} decks  win rate {rate,5:0}%  played {c.Plays,5} times");
            }

            sb.AppendLine();
            sb.AppendLine($"Troops stuck far from their target: {totalStuck}   Waiting in a crowd: {totalCrowded}   Timeouts: {totalTimeouts}   Errors logged: {h.ErrorCount}");
            h.WriteFile("ai_summary.txt", sb.ToString());
            h.Report(sb.ToString());
        }

        private SideTally Side(Difficulty difficulty)
        {
            if (!sides.TryGetValue(difficulty, out var side)) sides[difficulty] = side = new SideTally();
            return side;
        }

        private CardTally Tally(string id)
        {
            if (!cards.TryGetValue(id, out var tally)) cards[id] = tally = new CardTally();
            return tally;
        }

        private static string DeckName(DeckCycle deck) => string.Join("+", deck.Cards.Select(c => c.id).OrderBy(id => id));

        private static float TowerHealth(BattleManager battle, Team team)
        {
            float current = 0f, max = 0f;
            foreach (var tower in battle.Towers)
            {
                if (tower == null || tower.Team != team) continue;
                max += tower.Health.Max;
                if (tower.IsAlive) current += tower.Health.Current;
            }

            return max > 0f ? current / max : 0f;
        }

        private static string F(float value, string format = "0.0") => value.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>Flags troops that want to walk somewhere but haven't moved for a few seconds (pathing bugs, crowding).</summary>
        private sealed class StuckWatch
        {
            private const float Interval = 1f;
            private const float Limit = 3f;

            private readonly AiTournament owner;
            private readonly int seed;
            private readonly Dictionary<Unit, (Vector2 position, float still)> watched = new Dictionary<Unit, (Vector2, float)>();
            private readonly HashSet<Unit> reported = new HashSet<Unit>();
            private readonly List<Unit> units = new List<Unit>();
            private readonly List<Unit> gone = new List<Unit>();
            private float nextSample;

            public StuckWatch(AiTournament owner, int seed)
            {
                this.owner = owner;
                this.seed = seed;
            }

            public int Count { get; private set; }
            public int Crowded { get; private set; }

            public void Sample(float now)
            {
                if (now < nextSample) return;
                nextSample = now + Interval;

                units.Clear();
                foreach (var c in BattleRegistry.All)
                {
                    if (c is Unit u && u.IsAlive) units.Add(u);
                }

                gone.Clear();
                foreach (var u in watched.Keys)
                {
                    if (u == null || !u.IsAlive) gone.Add(u);
                }

                foreach (var u in gone) watched.Remove(u);

                foreach (var u in units)
                {
                    var target = u.Target;
                    bool wantsToMove = !u.IsDeploying && !u.IsAttacking && target != null && target.IsAlive &&
                                       u.EdgeDistanceTo(target) > u.Card.attackRange + 0.05f;

                    if (!watched.TryGetValue(u, out var entry))
                    {
                        watched[u] = (u.Position, 0f);
                        continue;
                    }

                    float moved = Vector2.Distance(entry.position, u.Position);
                    float still = wantsToMove && moved < 0.15f ? entry.still + Interval : 0f;
                    watched[u] = (u.Position, still);

                    if (still < Limit || !reported.Add(u)) continue;

                    // Waiting in a crowd right next to the target is normal; standing still far from it is a pathing bug.
                    float gap = u.EdgeDistanceTo(target);
                    if (gap < 1.2f)
                    {
                        Crowded++;
                        continue;
                    }

                    Count++;
                    if (owner.stuckReports++ < 40)
                    {
                        owner.h.Report($"    stuck (seed {seed}, {now:0}s): {u.name} at {u.Position.x:0.0},{u.Position.y:0.0} wants " +
                                       $"{target.name} at {target.Position.x:0.0},{target.Position.y:0.0} (gap {gap:0.00}) [{u.DebugState}]");
                    }
                }
            }
        }
    }
}
#endif
