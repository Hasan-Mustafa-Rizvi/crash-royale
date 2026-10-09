using System.Collections;
using System.Collections.Generic;
using CrashRoyale.Data;
using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>
    /// The opponent. It has its own deck and elixir and deploys through the same <see cref="BattleManager.TryDeploy"/>
    /// call as the player, so it can't cheat on cost, card cycle or placement.
    ///
    /// Every think step it does the first thing that applies:
    /// <list type="number">
    /// <item>Defend: an enemy push is coming and the towers alone would take real damage (a quick damage race decides).</item>
    /// <item>Pocket: we took a princess tower, so drop a building-hitter straight into the gap.</item>
    /// <item>Support: one of our troops is heading over the river, so back it up.</item>
    /// <item>Punish (Hard): the opponent just emptied their elixir, so rush a lane.</item>
    /// <item>Push: enough elixir saved, so start a push from the back, tank first.</item>
    /// <item>Don't leak: sitting at full elixir, so play the cheapest card.</item>
    /// </list>
    /// Difficulty changes how fast it reacts, how much tower damage it lets through, how well it picks counters and how
    /// tidy its placements are. Easy also plays the occasional random card.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        public sealed class Tuning
        {
            public float ThinkInterval;
            public float ReactionTime;
            public float Tolerance;
            public float PushAt;
            public float CounterAccuracy;
            public float RandomPlayChance;
            public float PlacementNoise;
            public bool SupportPushes;
            public bool UsePocket;
            public bool Punish;
            public bool ReadsTheClock;
            public float EmoteChance;
        }

        public static readonly Tuning Easy = new Tuning
        {
            ThinkInterval = 1.5f, ReactionTime = 1.6f, Tolerance = 380f, PushAt = 6f, CounterAccuracy = 0.35f,
            RandomPlayChance = 0.35f, PlacementNoise = 1.0f, EmoteChance = 0.5f
        };

        public static readonly Tuning Normal = new Tuning
        {
            ThinkInterval = 0.8f, ReactionTime = 0.8f, Tolerance = 230f, PushAt = 8f, CounterAccuracy = 0.8f,
            RandomPlayChance = 0.05f, PlacementNoise = 0.45f, SupportPushes = true, UsePocket = true, EmoteChance = 0.35f
        };

        public static readonly Tuning Hard = new Tuning
        {
            ThinkInterval = 0.45f, ReactionTime = 0.2f, Tolerance = 200f, PushAt = 7f, CounterAccuracy = 1f,
            PlacementNoise = 0.2f, SupportPushes = true, UsePocket = true, Punish = true, ReadsTheClock = true, EmoteChance = 0.25f
        };

        public static Tuning For(Difficulty difficulty) => difficulty == Difficulty.Easy ? Easy : difficulty == Difficulty.Hard ? Hard : Normal;

        /// <summary>An enemy group heading for one of our towers.</summary>
        private struct Threat
        {
            public Unit Lead;
            public Tower Tower;
            public float Health;
            public float BuildingDps;
            public float Burst;
            public float Speed;
            public float Elixir;
            public int Count;
            public bool HasTank;
            public bool HasBackline;
        }

        private readonly Dictionary<Unit, float> firstSeen = new Dictionary<Unit, float>();
        private readonly HashSet<Unit> supported = new HashSet<Unit>();
        private readonly List<Unit> ours = new List<Unit>();
        private readonly List<Unit> theirs = new List<Unit>();
        private readonly List<Tower> ourTowers = new List<Tower>();
        private readonly List<Tower> theirTowers = new List<Tower>();
        private readonly List<Unit> removeBuffer = new List<Unit>();

        private Team team;
        private Team enemy;
        private BattleManager battle;
        private ArenaLayout layout;
        private Tuning tuning;
        private System.Random rng;
        private bool running;
        private float nextThink;
        private int preferredLane;

        public Team Team => team;
        public Difficulty Difficulty { get; private set; }

        /// <summary>What the AI did last (shown by the dev tools).</summary>
        public string LastAction { get; private set; } = "";

        public void Begin(Team side, Difficulty difficulty, BattleManager manager, int seed)
        {
            Unsubscribe();

            team = side;
            enemy = side.Opponent();
            battle = manager;
            layout = manager.Layout;
            Difficulty = difficulty;
            tuning = For(difficulty);
            rng = new System.Random(seed);
            preferredLane = rng.Next(2);
            nextThink = 0f;
            running = true;
            enabled = true;

            battle.CrownsChanged += OnCrownsChanged;
            battle.BattleEnded += OnBattleEnded;
        }

        public void Stop() => running = false;

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (battle == null) return;
            battle.CrownsChanged -= OnCrownsChanged;
            battle.BattleEnded -= OnBattleEnded;
        }

        private void Update()
        {
            if (!running || battle == null || !battle.IsRunning || battle.Paused) return;
            if (Time.time < nextThink) return;

            nextThink = Time.time + tuning.ThinkInterval * Range(0.8f, 1.25f);
            Think();
        }

        private void Think()
        {
            BattleRegistry.GetUnits(team, ours);
            BattleRegistry.GetUnits(enemy, theirs);
            BattleRegistry.GetTowers(team, ourTowers);
            BattleRegistry.GetTowers(enemy, theirTowers);
            TrackThreats();

            var bank = battle.Bank(team);
            if (rng.NextDouble() < tuning.RandomPlayChance * tuning.ThinkInterval * 0.25 && TryRandomPlay(bank)) return;
            if (TryDefend(bank)) return;
            if (TryPocket(bank)) return;
            if (TrySupport(bank)) return;
            if (TryPunish(bank)) return;
            if (TryPush(bank)) return;
            TryAvoidLeak(bank);
        }

        // ---------------------------------------------------------------- defense

        private void TrackThreats()
        {
            removeBuffer.Clear();
            foreach (var pair in firstSeen)
            {
                if (pair.Key == null || !pair.Key.IsAlive || !IsThreat(pair.Key)) removeBuffer.Add(pair.Key);
            }

            foreach (var u in removeBuffer) firstSeen.Remove(u);

            foreach (var u in theirs)
            {
                if (IsThreat(u) && !firstSeen.ContainsKey(u)) firstSeen[u] = Time.time;
            }

            supported.RemoveWhere(u => u == null || !u.IsAlive);
        }

        /// <summary>Enemy troops on our half or on a bridge. Hard also watches troops lining up at the far end of a bridge.</summary>
        private bool IsThreat(Unit u)
        {
            if (u == null || u.IsDeploying) return false;
            if (layout.IsOnSide(team, u.Position)) return true;

            float fromRiver = Mathf.Abs(u.Position.y - layout.RiverY);
            float watch = Difficulty == Difficulty.Hard ? layout.riverHalfThickness + 0.9f : layout.riverHalfThickness + 0.1f;
            return fromRiver < watch;
        }

        private bool TryDefend(ElixirBank bank)
        {
            if (!FindThreat(out var threat)) return false;

            float expected = ExpectedTowerDamage(threat);
            if (expected < tuning.Tolerance) return false;

            int index = ChooseCounter(bank, threat);
            if (index < 0)
            {
                // Nothing affordable that helps yet. For a big threat, hold the elixir for defense instead of
                // spending it on offense.
                LastAction = "save for defense";
                return expected > tuning.Tolerance * 2f;
            }

            var card = battle.Deck(team).Hand(index);
            LastAction = $"defend {threat.Lead.Card.id} with {card.id}";
            return Deploy(index, DefensePosition(card, threat));
        }

        /// <summary>The enemy group closest to one of our towers that we've been watching long enough to react to.</summary>
        private bool FindThreat(out Threat threat)
        {
            threat = default;
            Unit lead = null;
            Tower tower = null;
            float closest = float.MaxValue;

            foreach (var u in theirs)
            {
                if (!firstSeen.TryGetValue(u, out float seen) || Time.time - seen < tuning.ReactionTime) continue;
                var t = TowerUnderThreat(u);
                if (t == null) continue;
                float d = Vector2.Distance(u.Position, t.Position);
                if (d < closest)
                {
                    closest = d;
                    lead = u;
                    tower = t;
                }
            }

            if (lead == null) return false;

            threat.Lead = lead;
            threat.Tower = tower;
            threat.Speed = float.MaxValue;
            foreach (var u in theirs)
            {
                if (Vector2.Distance(u.Position, lead.Position) > 2.6f) continue;
                var c = u.Card;
                threat.Count++;
                threat.Health += u.Health.Current;
                threat.Elixir += Value(u);
                threat.Speed = Mathf.Min(threat.Speed, c.moveSpeed);
                threat.HasTank |= c.role == CardRole.Tank;
                threat.HasBackline |= c.role == CardRole.Ranged || c.role == CardRole.Splash;
                if (c.attackKind == AttackKind.Kamikaze) threat.Burst += c.damage;
                else threat.BuildingDps += c.damage / Mathf.Max(0.1f, c.hitSpeed);
            }

            return true;
        }

        /// <summary>The tower this troop will most likely hit: the nearest of ours by walking distance.</summary>
        private Tower TowerUnderThreat(Unit u)
        {
            Tower best = null;
            float bestLength = float.MaxValue;
            foreach (var t in ourTowers)
            {
                float length = layout.PathLength(u.Position, t.Position);
                if (length < bestLength)
                {
                    bestLength = length;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>
        /// A quick damage race: how long until our towers and troops kill the group, against how long until it reaches
        /// the tower and starts hitting. The answer is roughly how much tower health we'd lose by doing nothing.
        /// </summary>
        private float ExpectedTowerDamage(in Threat threat)
        {
            float defense = DefenseDps(threat.Lead.Position, threat.Tower);
            float killTime = threat.Health / Mathf.Max(1f, defense);

            float reach = threat.Lead.Card.TargetsTroops ? Mathf.Max(threat.Lead.Card.attackRange, 0.3f) : 0.3f;
            float distance = Mathf.Max(0f, Vector2.Distance(threat.Lead.Position, threat.Tower.Position) - threat.Tower.Radius - reach);
            float arrival = distance / Mathf.Max(0.1f, threat.Speed);
            float hitting = Mathf.Max(0f, killTime - arrival);

            // Damage falls off as the group dies, so count roughly 60% of the full rate.
            float damage = threat.BuildingDps * hitting * 0.6f;
            if (threat.Burst > 0f && arrival < killTime) damage += threat.Burst;
            return damage;
        }

        private float DefenseDps(Vector2 at, Tower guard)
        {
            float dps = 0f;
            foreach (var u in ours)
            {
                if (!u.Card.TargetsTroops || Vector2.Distance(u.Position, at) > 4f) continue;
                float hit = u.Card.damage / Mathf.Max(0.1f, u.Card.hitSpeed);
                dps += u.Card.splashRadius > 0f ? hit * 1.4f : hit;
            }

            foreach (var t in ourTowers)
            {
                bool covers = t == guard || Vector2.Distance(t.Position, at) < t.Range + 1.5f;
                if (covers && (t.Active || !t.IsKing)) dps += t.Dps;
            }

            return dps;
        }

        private int ChooseCounter(ElixirBank bank, in Threat threat)
        {
            var deck = battle.Deck(team);
            int best = -1;
            float bestScore = 0.4f;

            for (int i = 0; i < deck.HandSize; i++)
            {
                var c = deck.Hand(i);
                if (c == null || !bank.CanAfford(c.elixirCost) || !c.TargetsTroops) continue;

                float score = 1f;
                switch (c.role)
                {
                    case CardRole.Splash:
                        score += threat.Count >= 3 ? 3f : threat.Count == 2 ? 1f : 0f;
                        if (threat.HasTank) score -= 0.5f;
                        break;
                    case CardRole.Swarm:
                        score += threat.HasTank ? 2.5f : 0f;
                        score += threat.HasBackline ? 1.2f : 0f;
                        if (threat.Count >= 3) score -= 1.8f;
                        break;
                    case CardRole.MeleeDps:
                        score += threat.HasTank ? 2.6f : 0.8f;
                        score += threat.HasBackline ? 1f : 0f;
                        break;
                    case CardRole.Ranged:
                        score += threat.HasTank ? 1.6f : 1.1f;
                        if (threat.Count >= 3) score -= 0.6f;
                        break;
                    case CardRole.Tank:
                        score += 0.2f;
                        break;
                }

                // Prefer a positive elixir trade.
                score -= Mathf.Max(0f, c.elixirCost - threat.Elixir - 0.5f) * 0.55f;
                score += Range(-1f, 1f) * (1f - tuning.CounterAccuracy) * 3f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        private Vector2 DefensePosition(CardData card, in Threat threat)
        {
            var lead = threat.Lead;
            Vector2 anchor = threat.Tower.Position;
            Vector2 toAnchor = anchor - lead.Position;
            Vector2 direction = toAnchor.sqrMagnitude > 0.0001f ? toAnchor.normalized : new Vector2(0f, -team.Forward());
            Vector2 position;

            if (card.attackKind == AttackKind.Melee)
            {
                // Meet the push inside tower range so the tower helps. Swarms go right on top of a tank.
                float distance = card.role == CardRole.Swarm && threat.HasTank ? 0.5f : Mathf.Clamp(toAnchor.magnitude * 0.45f, 0.6f, 1.6f);
                position = lead.Position + direction * distance;
            }
            else if (!threat.Tower.IsKing)
            {
                // Ranged defenders stand behind and inside the tower so the tower tanks for them.
                position = anchor + new Vector2(Mathf.Sign(layout.Center.x - anchor.x) * 1.15f, -team.Forward() * 0.5f);
            }
            else
            {
                position = lead.Position + direction * 2.4f;
            }

            return layout.ClampToOwnSide(team, position + Noise());
        }

        // ---------------------------------------------------------------- offense

        /// <summary>After taking a princess tower, building-hitters can be dropped past the river, right at the king.</summary>
        private bool TryPocket(ElixirBank bank)
        {
            if (!tuning.UsePocket) return false;

            var deck = battle.Deck(team);
            for (int lane = 0; lane < 2; lane++)
            {
                if (!layout.PocketOpen(team, lane)) continue;

                for (int i = 0; i < deck.HandSize; i++)
                {
                    var c = deck.Hand(i);
                    if (c == null || c.TargetsTroops || !bank.CanAfford(c.elixirCost)) continue;

                    float forward = team.Forward();
                    float depth = layout.riverHalfThickness + layout.pocketDepth - 0.35f;
                    float x = layout.Center.x + (layout.BridgeX(lane) - layout.Center.x) * 0.55f;
                    var spot = new Vector2(x, layout.RiverY + forward * depth) + Noise() * 0.4f;
                    if (!layout.IsDeployable(team, spot)) continue;

                    LastAction = $"pocket {c.id}";
                    return battle.TryDeploy(team, i, spot);
                }
            }

            return false;
        }

        /// <summary>Back up one of our troops that is heading over the river (a fresh push or survivors of a defense).</summary>
        private bool TrySupport(ElixirBank bank)
        {
            if (!tuning.SupportPushes || bank.Current < 3.5f) return false;

            Unit lead = null;
            float best = float.MinValue;
            foreach (var u in ours)
            {
                if (u.IsDeploying || supported.Contains(u)) continue;
                bool tank = u.Card.role == CardRole.Tank;
                if (!tank && (u.Health.Fraction < 0.6f || u.Card.attackKind == AttackKind.Kamikaze)) continue;

                float progress = (u.Position.y - layout.RiverY) * team.Forward();
                if (progress < -3.2f || progress > 2.5f) continue;

                float score = progress + (tank ? 3f : 0f) + u.Card.elixirCost * 0.3f;
                if (score > best)
                {
                    best = score;
                    lead = u;
                }
            }

            if (lead == null) return false;
            bool leadIsTank = lead.Card.role == CardRole.Tank;
            if (!leadIsTank && bank.Current < 5f) return false;

            var deck = battle.Deck(team);
            int index = -1;
            float bestScore = 0f;
            for (int i = 0; i < deck.HandSize; i++)
            {
                var c = deck.Hand(i);
                if (c == null || !bank.CanAfford(c.elixirCost) || c.role == CardRole.Tank) continue;

                float score = c.role == CardRole.Ranged || c.role == CardRole.Splash ? 2f
                    : c.role == CardRole.MeleeDps ? 1.6f
                    : c.role == CardRole.Kamikaze ? 1.4f
                    : 1f;
                if (!leadIsTank) score -= 0.5f;
                score += Range(-0.4f, 0.4f);
                if (score > bestScore)
                {
                    bestScore = score;
                    index = i;
                }
            }

            if (index < 0) return false;

            var card = deck.Hand(index);
            float behind = card.attackKind == AttackKind.Kamikaze ? -0.4f : card.attackRange > 1f ? 1.4f : 0.9f;
            Vector2 position = lead.Position - new Vector2(0f, team.Forward() * behind);
            if (!layout.IsDeployable(team, position))
            {
                int lane = layout.LaneOf(lead.Position);
                position = new Vector2(layout.BridgeX(lane), layout.RiverY - team.Forward() * (layout.riverHalfThickness + 0.7f));
            }

            supported.Add(lead);
            LastAction = $"support {lead.Card.id} with {card.id}";
            return Deploy(index, layout.ClampToOwnSide(team, position + Noise() * 0.5f));
        }

        /// <summary>Hard reads the opponent's elixir: right after a big spend, a fast push gets through cheaply.</summary>
        private bool TryPunish(ElixirBank bank)
        {
            if (!tuning.Punish || bank.Current < 5f || battle.Bank(enemy).Current > 2.5f) return false;

            var deck = battle.Deck(team);
            int index = -1;
            float bestScore = 0f;
            for (int i = 0; i < deck.HandSize; i++)
            {
                var c = deck.Hand(i);
                if (c == null || !bank.CanAfford(c.elixirCost)) continue;
                float score = c.attackKind == AttackKind.Kamikaze ? 3f : c.moveSpeed >= 0.8f ? 2.4f : c.role == CardRole.MeleeDps ? 1.8f : 0f;
                if (score > bestScore)
                {
                    bestScore = score;
                    index = i;
                }
            }

            if (index < 0) return false;

            int lane = ChooseLane();
            var position = new Vector2(layout.BridgeX(lane), layout.RiverY - team.Forward() * (layout.riverHalfThickness + 0.6f));
            LastAction = $"punish with {deck.Hand(index).id}";
            return Deploy(index, layout.ClampToOwnSide(team, position + Noise()));
        }

        private bool TryPush(ElixirBank bank)
        {
            if (bank.Current < PushThreshold()) return false;

            var deck = battle.Deck(team);
            int index = -1;
            float bestScore = 0f;
            for (int i = 0; i < deck.HandSize; i++)
            {
                var c = deck.Hand(i);
                if (c == null || !bank.CanAfford(c.elixirCost)) continue;

                float score = c.role switch
                {
                    CardRole.Tank => 3f,
                    CardRole.MeleeDps => 1.8f,
                    CardRole.Kamikaze => 1.6f,
                    CardRole.Swarm => 1.2f,
                    _ => 1f
                };
                score += Range(-0.5f, 0.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    index = i;
                }
            }

            if (index < 0) return false;

            var card = deck.Hand(index);
            LastAction = $"push with {card.id}";
            return Deploy(index, PushPosition(card, ChooseLane()));
        }

        private float PushThreshold()
        {
            float threshold = tuning.PushAt;
            if (battle.DoubleElixir) threshold -= 1.5f;

            if (tuning.ReadsTheClock && battle.TimeRemaining < 40f)
            {
                int us = battle.Crowns(team), them = battle.Crowns(enemy);
                if (us < them) threshold -= 2.5f;
                else if (us > them) threshold += 2f;
            }

            return Mathf.Clamp(threshold, 3f, 9.8f);
        }

        private void TryAvoidLeak(ElixirBank bank)
        {
            if (bank.Current < ElixirBank.Max - 0.4f) return;

            var deck = battle.Deck(team);
            int cheapest = -1;
            for (int i = 0; i < deck.HandSize; i++)
            {
                var c = deck.Hand(i);
                if (c != null && (cheapest < 0 || c.elixirCost < deck.Hand(cheapest).elixirCost)) cheapest = i;
            }

            if (cheapest < 0) return;
            LastAction = $"dump {deck.Hand(cheapest).id}";
            Deploy(cheapest, PushPosition(deck.Hand(cheapest), ChooseLane()));
        }

        /// <summary>Easy's habit: every so often it just plays whatever, wherever.</summary>
        private bool TryRandomPlay(ElixirBank bank)
        {
            var deck = battle.Deck(team);
            int index = rng.Next(deck.HandSize);
            var card = deck.Hand(index);
            if (card == null || !bank.CanAfford(card.elixirCost)) return false;

            float forward = team.Forward();
            var spot = new Vector2(Range(-layout.halfWidth + 0.6f, layout.halfWidth - 0.6f), layout.RiverY - forward * Range(1f, layout.halfHeight - 0.8f));
            LastAction = $"random {card.id}";
            return Deploy(index, layout.ClampToOwnSide(team, spot));
        }

        private Vector2 PushPosition(CardData card, int lane)
        {
            float forward = team.Forward();
            float laneSign = lane == 0 ? -1f : 1f;
            float riverEdge = layout.riverHalfThickness;
            Vector2 position;

            switch (card.role)
            {
                case CardRole.Tank:
                    bool atBridge = Difficulty == Difficulty.Hard && rng.NextDouble() < 0.35;
                    position = atBridge
                        ? new Vector2(layout.BridgeX(lane), layout.RiverY - forward * (riverEdge + 0.8f))
                        : layout.TowerSpot(team, true, -1) + new Vector2(laneSign * layout.laneX * 0.7f, forward * 1.1f);
                    break;
                case CardRole.Kamikaze:
                    position = new Vector2(layout.BridgeX(lane), layout.RiverY - forward * (riverEdge + 0.6f));
                    break;
                case CardRole.Ranged:
                case CardRole.Splash:
                    // Back-liners start behind the princess tower and walk up, which gives the push time to build.
                    position = layout.TowerSpot(team, false, lane) + new Vector2(-laneSign * 0.9f, -forward * 0.6f);
                    break;
                default:
                    position = new Vector2(layout.BridgeX(lane) * 0.9f, layout.RiverY - forward * (riverEdge + 1.6f));
                    break;
            }

            return layout.ClampToOwnSide(team, position + Noise());
        }

        private int ChooseLane()
        {
            Tower left = null, right = null;
            foreach (var t in theirTowers)
            {
                if (t.IsKing) continue;
                if (t.Lane == 0) left = t;
                else right = t;
            }

            // A missing princess tower means the king is exposed in that lane.
            if (left == null && right != null) return 0;
            if (right == null && left != null) return 1;
            if (left != null && right != null)
            {
                float diff = left.Health.Fraction - right.Health.Fraction;
                if (Mathf.Abs(diff) > 0.12f) return diff < 0f ? 0 : 1;
            }

            if (rng.NextDouble() < 0.2) preferredLane = 1 - preferredLane;
            return preferredLane;
        }

        // ---------------------------------------------------------------- helpers

        private bool Deploy(int handIndex, Vector2 position)
        {
            if (battle.TryDeploy(team, handIndex, position)) return true;
            return battle.TryDeploy(team, handIndex, layout.ClampToOwnSide(team, position));
        }

        private static float Value(Unit u)
        {
            return u.Card.elixirCost / (float)Mathf.Max(1, u.Card.unitCount) * (0.35f + 0.65f * u.Health.Fraction);
        }

        private Vector2 Noise() => new Vector2(Range(-1f, 1f), Range(-1f, 1f)) * tuning.PlacementNoise;

        private float Range(float min, float max) => (float)(min + rng.NextDouble() * (max - min));

        // ---------------------------------------------------------------- emotes

        private void OnCrownsChanged(Team scorer, int crowns)
        {
            if (!running || rng.NextDouble() > tuning.EmoteChance) return;
            string emote = scorer == team ? "laugh" : rng.NextDouble() < 0.5 ? "angry" : "cry";
            StartCoroutine(EmoteAfter(emote, 0.6f));
        }

        private void OnBattleEnded(BattleResult result)
        {
            bool won = (team == Team.Blue && result.Outcome == BattleOutcome.BlueWins) ||
                       (team == Team.Red && result.Outcome == BattleOutcome.RedWins);
            if (rng != null && rng.NextDouble() < 0.7) StartCoroutine(EmoteAfter(won ? "laugh" : "gg", 1.1f));
        }

        private IEnumerator EmoteAfter(string emoteId, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (battle != null) battle.ShowEmote(team, emoteId);
        }
    }
}
