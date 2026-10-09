using System.Collections.Generic;
using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>Every troop and tower currently in the battle. Queries filter by team so registration order never matters.</summary>
    public static class BattleRegistry
    {
        private static readonly List<Combatant> all = new List<Combatant>(64);
        private static readonly List<Combatant> scratch = new List<Combatant>(64);

        public static IReadOnlyList<Combatant> All => all;

        public static void Add(Combatant combatant)
        {
            if (combatant != null && !all.Contains(combatant)) all.Add(combatant);
        }

        public static void Remove(Combatant combatant) => all.Remove(combatant);

        public static void Clear() => all.Clear();

        /// <summary>Drops entries whose objects are gone. Safe at any time, unlike Clear().</summary>
        public static void Prune() => all.RemoveAll(c => c == null);

        /// <summary>Copy of the live list, safe to iterate while things die.</summary>
        public static List<Combatant> Snapshot()
        {
            scratch.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null) scratch.Add(all[i]);
            }

            return scratch;
        }

        public static Combatant FindNearestEnemy(Team team, Vector2 from, float maxEdgeDistance, bool troops, bool buildings)
        {
            Combatant best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.Team == team || !c.IsAlive) continue;
                if (c.IsBuilding ? !buildings : !troops) continue;

                float d = Vector2.Distance(from, c.Position) - c.Radius;
                if (d <= maxEdgeDistance && d < bestDistance)
                {
                    best = c;
                    bestDistance = d;
                }
            }

            return best;
        }

        public static int CountUnits(Team team)
        {
            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.Team == team && !c.IsBuilding && c.IsAlive) count++;
            }

            return count;
        }

        public static void GetUnits(Team team, List<Unit> results)
        {
            results.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is Unit u && u.Team == team && u.IsAlive) results.Add(u);
            }
        }

        public static void GetTowers(Team team, List<Tower> results, bool aliveOnly = true)
        {
            results.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is Tower t && t.Team == team && (!aliveOnly || t.IsAlive)) results.Add(t);
            }
        }
    }
}
