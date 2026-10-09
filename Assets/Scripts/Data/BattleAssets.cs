using CrashRoyale.Battle;
using UnityEngine;

namespace CrashRoyale.Data
{
    /// <summary>Prefabs the battle spawns at runtime.</summary>
    [CreateAssetMenu(menuName = "Crash Royale/Battle Assets", fileName = "BattleAssets")]
    public class BattleAssets : ScriptableObject
    {
        public CardDatabase database;
        public Unit unitPrefab;

        [Header("Effects")]
        public GameObject spawnFx;
        public GameObject deathFx;
        public GameObject explosionFx;
        public GameObject towerExplosionFx;
        public GameObject hitFx;
    }
}
