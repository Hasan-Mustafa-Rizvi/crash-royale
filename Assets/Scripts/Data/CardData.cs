using CrashRoyale.Battle;
using UnityEngine;

namespace CrashRoyale.Data
{
    public enum TargetMode
    {
        Any,
        BuildingsOnly
    }

    public enum AttackKind
    {
        Melee,
        Projectile,
        Kamikaze
    }

    /// <summary>What a card is good at. The AI uses this to pick counters and to build pushes.</summary>
    public enum CardRole
    {
        Tank,
        Swarm,
        MeleeDps,
        Splash,
        Ranged,
        Kamikaze
    }

    public enum Rarity
    {
        Common,
        Rare,
        Epic
    }

    /// <summary>Everything that defines a card: cost, the troop it spawns and that troop's stats.</summary>
    [CreateAssetMenu(menuName = "Crash Royale/Card", fileName = "Card")]
    public class CardData : ScriptableObject
    {
        [Header("Card")]
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite portrait;
        [Tooltip("Shown when the portrait's art isn't installed (the optional Lancer art, for example).")]
        public Sprite fallbackPortrait;
        public Rarity rarity;
        public CardRole role;
        [Range(1, 10)] public int elixirCost = 3;
        public bool unlockedByDefault = true;
        [Min(0)] public int unlockPrice;

        [Header("Spawn")]
        [Min(1)] public int unitCount = 1;
        public float formationSpacing = 0.45f;
        public float deployTime = 1f;

        [Header("Stats")]
        public int maxHealth = 500;
        public int damage = 100;
        [Tooltip("Seconds between attacks.")] public float hitSpeed = 1.2f;
        [Tooltip("Edge-to-edge distance the unit can attack from.")] public float attackRange = 0.4f;
        public float sightRange = 4f;
        public float moveSpeed = 0.6f;
        public float radius = 0.22f;
        public float mass = 1f;
        public TargetMode targets = TargetMode.Any;
        public AttackKind attackKind = AttackKind.Melee;
        [Tooltip("0 = single target.")] public float splashRadius;
        public Projectile projectilePrefab;
        public Vector2 projectileOrigin = new Vector2(0.15f, 0.35f);

        [Header("Visuals")]
        public UnitSkin blueSkin;
        public UnitSkin redSkin;
        [Tooltip("Used when the primary skin's art is not installed.")]
        public UnitSkin fallbackBlueSkin;
        public UnitSkin fallbackRedSkin;
        [Tooltip("Tint for the fallback skin, so a stand-in troop doesn't look exactly like the card it borrows from.")]
        public Color fallbackTint = Color.white;
        public float visualScale = 1f;

        [Header("Audio")]
        public string attackSfx;
        public string impactSfx;

        public float DamagePerSecond => damage / Mathf.Max(0.1f, hitSpeed);

        public bool TargetsTroops => targets == TargetMode.Any;

        /// <summary>True when the primary art is missing and the troop borrows another card's look.</summary>
        public bool UsesFallback(Team team)
        {
            var primary = team == Team.Blue ? blueSkin : redSkin;
            return !(primary != null && primary.IsValid) && (team == Team.Blue ? fallbackBlueSkin : fallbackRedSkin) != null;
        }

        public UnitSkin SkinFor(Team team)
        {
            var primary = team == Team.Blue ? blueSkin : redSkin;
            if (primary != null && primary.IsValid) return primary;

            var fallback = team == Team.Blue ? fallbackBlueSkin : fallbackRedSkin;
            return fallback != null && fallback.IsValid ? fallback : primary;
        }

        /// <summary>Idle frame used for previews (deploy ghost, cards).</summary>
        public Sprite PreviewSprite(Team team)
        {
            var skin = SkinFor(team);
            return skin != null && skin.idle.IsValid ? skin.idle.frames[0] : Portrait;
        }

        public Sprite Portrait => portrait != null ? portrait : fallbackPortrait;
    }
}
