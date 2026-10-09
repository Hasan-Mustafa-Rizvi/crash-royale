using UnityEngine;

namespace CrashRoyale.Battle
{
    public static class Combat
    {
        /// <summary>Damages every enemy whose footprint overlaps the circle. Returns how many were hit.</summary>
        public static int DamageArea(Vector2 center, float radius, int damage, Team attacker, bool troops = true, bool buildings = true)
        {
            int hits = 0;
            var targets = BattleRegistry.Snapshot();
            for (int i = 0; i < targets.Count; i++)
            {
                var c = targets[i];
                if (c == null || c.Team == attacker || !c.IsAlive) continue;
                if (c.IsBuilding ? !buildings : !troops) continue;
                if (Vector2.Distance(center, c.Position) - c.Radius > radius) continue;

                c.Health.TakeDamage(damage);
                hits++;
            }

            return hits;
        }
    }
}
