using CrashRoyale.Audio;
using CrashRoyale.Visual;
using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>
    /// Princess and king towers. Princess towers shoot from the start; the king sleeps until it takes damage
    /// or one of its princess towers falls, like in the real thing.
    /// </summary>
    public class Tower : Combatant
    {
        public enum Kind
        {
            Princess,
            King
        }

        [SerializeField] private Kind kind = Kind.Princess;
        [SerializeField] private int lane = -1;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Sprite blueSprite;
        [SerializeField] private Sprite redSprite;
        [SerializeField] private Sprite destroyedSprite;
        [SerializeField] private WorldHealthBar healthBar;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject sleepIndicator;

        [Header("Stats")]
        [SerializeField] private int maxHealth = 1400;
        [SerializeField] private int damage = 50;
        [SerializeField] private float hitSpeed = 0.8f;
        [SerializeField] private float range = 3.7f;
        [SerializeField] private float aimHeight = 0.9f;

        private Combatant target;
        private float cooldown;
        private bool initialized;

        public Kind TowerKind => kind;
        public bool IsKing => kind == Kind.King;
        public int Lane => lane;
        public float Range => range;
        public float Dps => damage / Mathf.Max(0.1f, hitSpeed);
        public bool Active { get; private set; }
        public Combatant CurrentTarget => target;
        public override bool IsBuilding => true;
        public override Vector2 AimPoint => Position + new Vector2(0f, aimHeight);

        public void Init()
        {
            if (initialized) return;
            initialized = true;

            Health.Init(maxHealth);
            Health.SetFlashTarget(body);
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;

            body.sprite = team == Team.Blue ? blueSprite : redSprite;
            healthBar.Bind(Health, team);

            Active = kind == Kind.Princess;
            if (sleepIndicator != null) sleepIndicator.SetActive(!Active);
            cooldown = hitSpeed * 0.5f;
        }

        public void Activate()
        {
            if (Active || !IsAlive) return;
            Active = true;
            if (sleepIndicator != null) sleepIndicator.SetActive(false);
        }

        /// <summary>Used when the king falls: the loser's remaining towers crumble too.</summary>
        public void ForceDestroy()
        {
            if (IsAlive) Health.TakeDamage(Health.Current);
        }

        private void OnDamaged(int amount)
        {
            if (IsKing) Activate();
        }

        private void Update()
        {
            if (!initialized || !Active || !IsAlive) return;

            var battle = BattleManager.Instance;
            if (battle == null || !battle.IsRunning) return;

            cooldown -= Time.deltaTime;

            if (target == null || !target.IsAlive || Vector2.Distance(Position, target.Position) - target.Radius > range)
                target = BattleRegistry.FindNearestEnemy(team, Position, range, true, false);

            if (target != null && cooldown <= 0f)
            {
                Fire();
                cooldown = hitSpeed;
            }
        }

        private void Fire()
        {
            if (projectilePrefab == null)
            {
                target.Health.TakeDamage(damage);
                return;
            }

            Vector2 origin = firePoint != null ? (Vector2)firePoint.position : AimPoint;
            var projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.Launch(team, target, damage, 0f);
            AudioManager.Play(Sfx.TowerShot);
        }

        private void OnDied()
        {
            target = null;
            body.sprite = destroyedSprite;

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            healthBar.Hide();
            if (sleepIndicator != null) sleepIndicator.SetActive(false);

            var battle = BattleManager.Instance;
            if (battle != null && battle.Assets != null)
                Fx.Spawn(battle.Assets.towerExplosionFx, AimPoint, IsKing ? 2.2f : 1.6f);

            AudioManager.Play(Sfx.TowerDestroyed);
            CameraRig.Shake(IsKing ? 0.35f : 0.2f, IsKing ? 0.5f : 0.35f);

            if (battle != null) battle.NotifyTowerDestroyed(this);
        }
    }
}
