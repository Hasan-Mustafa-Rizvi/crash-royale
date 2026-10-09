using CrashRoyale.Audio;
using CrashRoyale.Data;
using CrashRoyale.Visual;
using UnityEngine;

namespace CrashRoyale.Battle
{
    public static class Deployer
    {
        public static void Spawn(BattleAssets assets, CardData card, Team team, Vector2 center)
        {
            float forward = team.Forward();
            var layout = ArenaLayout.Instance;

            for (int i = 0; i < card.unitCount; i++)
            {
                Vector2 position = center + Formation(card.unitCount, i, card.formationSpacing, forward);
                if (layout != null && !layout.InField(position, 0.15f)) position = center;

                var unit = Object.Instantiate(assets.unitPrefab, position, Quaternion.identity);
                unit.Init(card, team, assets);
            }

            Fx.Spawn(assets.spawnFx, center, card.unitCount > 1 ? 1.25f : 1f);
            AudioManager.Play(Sfx.Deploy);
        }

        /// <summary>Offsets for multi-unit cards: pairs side by side, trios in a triangle pointing at the enemy.</summary>
        public static Vector2 Formation(int count, int index, float spacing, float forward)
        {
            switch (count)
            {
                case 1:
                    return Vector2.zero;
                case 2:
                    return new Vector2(index == 0 ? -spacing * 0.5f : spacing * 0.5f, 0f);
                case 3:
                    if (index == 0) return new Vector2(0f, spacing * 0.45f * forward);
                    return new Vector2(index == 1 ? -spacing * 0.55f : spacing * 0.55f, -spacing * 0.3f * forward);
                default:
                    float angle = index * Mathf.PI * 2f / count;
                    return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spacing * 0.6f;
            }
        }
    }
}
