using CrashRoyale.Audio;
using CrashRoyale.Data;
using CrashRoyale.Visual;
using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>
    /// A deployed troop. Picks the nearest enemy it can see (or the nearest tower down its lane), walks there through
    /// the bridges and attacks on the hit frame of its animation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Unit : Combatant
    {
        private enum State
        {
            Deploying,
            Active,
            Dead
        }

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private WorldHealthBar healthBar;
        [SerializeField] private float healthBarHeight = 1.02f;

        /// <summary>
        /// Troops don't collide with each other through the physics engine (two tanks meeting head-on on a narrow bridge
        /// would lock up forever). They push each other apart softly instead, heavier troops shoving lighter ones,
        /// and still collide normally with walls and towers.
        /// </summary>
        public const int TroopLayer = 8;
        private const float SeparationStrength = 9f;
        private const float AvoidLookAhead = 0.85f;

        private Rigidbody2D rb;
        private CircleCollider2D circle;
        private UnitSkin skin;
        private BattleAssets assets;
        private State state;
        private Combatant target;
        private Combatant attackTarget;
        private float deployTimer;
        private float cooldown;
        private float retargetTimer;
        private float attackTimer;
        private bool attacking;
        private bool attackApplied;
        private int bridge = -1;
        private Vector2 desiredVelocity;
        private float blockedTime;
        private float squeezeTime;
        private float scale = 1f;

        public CardData Card { get; private set; }
        public Combatant Target => target;
        public bool IsDeploying => state == State.Deploying;
        internal bool IsAttacking => attacking;
        internal string DebugState => $"want {desiredVelocity.x:0.00},{desiredVelocity.y:0.00} moving {(rb != null ? rb.linearVelocity.magnitude : 0f):0.00} squeeze {squeezeTime:0.0} bridge {bridge}";
        public override bool IsBuilding => false;
        public override Vector2 AimPoint => Position + new Vector2(0f, 0.42f * scale);

        public void Init(CardData card, Team owner, BattleAssets battleAssets)
        {
            Card = card;
            team = owner;
            assets = battleAssets;
            radius = card.radius;
            scale = card.visualScale;
            name = $"{card.displayName} ({owner})";
            gameObject.layer = TroopLayer;

            rb = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            circle.radius = radius;
            rb.gravityScale = 0f;
            rb.mass = card.mass;
            rb.linearDamping = 6f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            skin = card.SkinFor(owner);
            body.flipX = owner == Team.Red;
            body.color = card.UsesFallback(owner) ? card.fallbackTint : Color.white;

            Health.Init(card.maxHealth);
            Health.SetFlashTarget(body);
            Health.Died += OnDied;
            healthBar.transform.localPosition = new Vector3(0f, healthBarHeight * scale, 0f);
            healthBar.Bind(Health, owner);

            animator.EventFrame += OnAnimationEvent;
            animator.Finished += OnAnimationFinished;
            if (skin != null) animator.Play(skin.idle);

            state = State.Deploying;
            deployTimer = Mathf.Max(0.05f, card.deployTime);
            retargetTimer = Random.Range(0f, 0.25f);
            body.transform.localScale = new Vector3(scale * 0.4f, scale * 0.4f, 1f);
            SetAlpha(0.6f);
        }

        private void Update()
        {
            if (state == State.Dead || Card == null) return;

            if (state == State.Deploying)
            {
                UpdateDeploy();
                return;
            }

            var battle = BattleManager.Instance;
            if (battle == null || !battle.IsRunning)
            {
                desiredVelocity = Vector2.zero;
                if (!attacking) animator.Play(skin.idle);
                return;
            }

            cooldown -= Time.deltaTime;
            retargetTimer -= Time.deltaTime;

            if (attacking)
            {
                desiredVelocity = Vector2.zero;
                attackTimer += Time.deltaTime;
                if (attackTimer > Card.hitSpeed * 2f + 0.5f) FinishAttack();
                return;
            }

            bool targetValid = target != null && target.IsAlive;
            if (!targetValid || (retargetTimer <= 0f && !InRange(target, 0f)))
            {
                AcquireTarget();
                retargetTimer = 0.3f;
            }

            if (target == null)
            {
                desiredVelocity = Vector2.zero;
                animator.Play(skin.idle);
                return;
            }

            if (InRange(target, 0f))
            {
                desiredVelocity = Vector2.zero;
                Face(target.Position - Position);
                if (cooldown <= 0f) BeginAttack();
                else animator.Play(skin.idle);
                return;
            }

            var layout = ArenaLayout.Instance;
            Vector2 waypoint = target.Position;
            if (layout != null)
            {
                waypoint = layout.Steer(Position, target.Position, ref bridge, radius);
                waypoint = layout.AroundTowers(Position, waypoint, radius, target);
            }

            Vector2 direction = waypoint - Position;
            if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
            bool nearRiver = layout != null && layout.InRiverBand(Position, 0.7f);
            if (squeezeTime <= 0f && !nearRiver) direction = AvoidBlockers(direction);

            desiredVelocity = direction * Card.moveSpeed;
            Face(direction);
            animator.Play(skin.run);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigurePhysics() => Physics2D.IgnoreLayerCollision(TroopLayer, TroopLayer, true);

        private void FixedUpdate()
        {
            if (state == State.Dead || rb == null) return;
            float dt = Time.fixedDeltaTime;

            // A troop that keeps trying to walk but goes nowhere squeezes past whatever is in its way for a moment.
            // This is what stops crowds jamming on the narrow bridges.
            float wanted = desiredVelocity.magnitude;
            if (wanted > 0.05f && rb.linearVelocity.magnitude < wanted * 0.35f) blockedTime += dt;
            else blockedTime = Mathf.Max(0f, blockedTime - dt);

            if (blockedTime > 0.75f)
            {
                squeezeTime = 1.2f;
                blockedTime = 0f;
            }

            if (squeezeTime > 0f) squeezeTime -= dt;

            float acceleration = Mathf.Max(4f, Card.moveSpeed * 14f);
            Vector2 push = Separation() * (squeezeTime > 0f ? 0.15f : 1f);
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity + push, acceleration * dt);
        }

        private Vector2 Body => rb != null ? rb.position : Position;

        /// <summary>Soft push away from overlapping troops. The lighter troop gives way more.</summary>
        private Vector2 Separation()
        {
            Vector2 push = Vector2.zero;
            Vector2 position = Body;
            var all = BattleRegistry.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Unit other) || other == this || other.state == State.Dead || other.Card == null) continue;

                Vector2 offset = position - other.Body;
                float min = radius + other.radius;
                float squared = offset.sqrMagnitude;
                if (squared >= min * min) continue;

                float distance = Mathf.Sqrt(squared);
                Vector2 normal = distance > 0.0001f
                    ? offset / distance
                    : new Vector2(GetInstanceID() > other.GetInstanceID() ? 1f : -1f, 0f);
                float share = other.Card.mass / (Card.mass + other.Card.mass);
                push += normal * ((min - distance) * share);
            }

            return push * SeparationStrength;
        }

        /// <summary>Steers around the nearest troop standing right in our path, unless it's light enough to shove.</summary>
        private Vector2 AvoidBlockers(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return direction;

            Unit blocker = null;
            float nearest = float.MaxValue;
            float blockerSide = 0f;
            var all = BattleRegistry.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Unit other) || other == this || other.state == State.Dead || other.Card == null || other == target) continue;
                if (other.Card.mass < Card.mass * 0.5f) continue;

                Vector2 offset = other.Position - Position;
                float along = Vector2.Dot(offset, direction);
                float clearance = radius + other.radius + 0.04f;
                if (along <= 0f || along > AvoidLookAhead + clearance) continue;

                float side = direction.x * offset.y - direction.y * offset.x;
                if (Mathf.Abs(side) >= clearance) continue;

                if (along < nearest)
                {
                    nearest = along;
                    blocker = other;
                    blockerSide = side;
                }
            }

            if (blocker == null) return direction;

            // Pass on the free side; when dead ahead, everyone keeps right so two troops never mirror each other.
            Vector2 left = new Vector2(-direction.y, direction.x);
            float pass = Mathf.Abs(blockerSide) > 0.03f ? -Mathf.Sign(blockerSide) : -1f;
            float closeness = 1f - Mathf.Clamp01((nearest - radius - blocker.radius) / AvoidLookAhead);
            return (direction + left * (pass * closeness * 1.3f)).normalized;
        }

        private void UpdateDeploy()
        {
            desiredVelocity = Vector2.zero;
            deployTimer -= Time.deltaTime;

            float progress = 1f - Mathf.Clamp01(deployTimer / Mathf.Max(0.05f, Card.deployTime));
            float pop = Mathf.Clamp01(progress * 5f);
            float settle = Mathf.Clamp01((progress - 0.2f) * 5f);
            float s = scale * (pop < 1f ? Mathf.Lerp(0.4f, 1.12f, pop) : Mathf.Lerp(1.12f, 1f, settle));
            body.transform.localScale = new Vector3(s, s, 1f);

            if (deployTimer > 0f) return;

            state = State.Active;
            body.transform.localScale = new Vector3(scale, scale, 1f);
            SetAlpha(1f);
            Health.SetFlashTarget(body);
        }

        private bool InRange(Combatant c, float slack) => c != null && EdgeDistanceTo(c) <= Card.attackRange + slack;

        private void AcquireTarget()
        {
            var best = BattleRegistry.FindNearestEnemy(team, Position, Card.sightRange, Card.TargetsTroops, true);
            if (best == null) best = NearestEnemyBuildingByPath();

            // Stick with a troop we're already chasing unless the new one is clearly closer. Flip-flopping between two
            // targets in different directions leaves a troop walking on the spot.
            if (target != null && target.IsAlive && best != null && best != target && !target.IsBuilding)
            {
                float current = EdgeDistanceTo(target);
                bool stillInSight = current <= Card.sightRange + 1f;
                bool muchCloser = EdgeDistanceTo(best) < current - 0.75f;
                if (stillInSight && !muchCloser) return;
            }

            target = best;
        }

        private Combatant NearestEnemyBuildingByPath()
        {
            var layout = ArenaLayout.Instance;
            Combatant best = null;
            float bestLength = float.MaxValue;
            var all = BattleRegistry.All;

            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!(c is Tower) || c.Team == team || !c.IsAlive) continue;
                float length = layout != null ? layout.PathLength(Position, c.Position) : Vector2.Distance(Position, c.Position);
                if (length < bestLength)
                {
                    bestLength = length;
                    best = c;
                }
            }

            return best;
        }

        private void BeginAttack()
        {
            attackTarget = target;
            cooldown = Card.hitSpeed;
            attackApplied = false;
            attackTimer = 0f;

            var anim = PickAttackAnim(target.Position - Position);
            if (anim == null || !anim.IsValid || anim.loop)
            {
                ApplyAttack();
                return;
            }

            attacking = true;
            float maxDuration = Card.hitSpeed * 0.9f;
            float playback = anim.Duration > maxDuration ? anim.Duration / maxDuration : 1f;
            animator.Play(anim, true, playback);
        }

        private SpriteAnim PickAttackAnim(Vector2 toTarget)
        {
            if (Card.attackKind == AttackKind.Kamikaze && skin.special.IsValid) return skin.special;

            float ax = Mathf.Abs(toTarget.x);
            float ay = Mathf.Abs(toTarget.y);
            if (ay > ax * 1.25f)
            {
                var vertical = toTarget.y > 0f ? skin.attackUp : skin.attackDown;
                if (vertical != null && vertical.IsValid) return vertical;
            }

            return skin.attackSide;
        }

        private void OnAnimationEvent()
        {
            if (attacking && !attackApplied) ApplyAttack();
        }

        private void OnAnimationFinished()
        {
            if (!attacking) return;
            if (!attackApplied) ApplyAttack();
            FinishAttack();
        }

        private void FinishAttack()
        {
            attacking = false;
            if (state != State.Dead) animator.Play(skin.idle);
        }

        private void ApplyAttack()
        {
            attackApplied = true;

            switch (Card.attackKind)
            {
                case AttackKind.Melee:
                    if (attackTarget == null || !attackTarget.IsAlive || EdgeDistanceTo(attackTarget) > Card.attackRange + 0.35f) return;

                    if (Card.splashRadius > 0f)
                        Combat.DamageArea(attackTarget.Position, Card.splashRadius, Card.damage, team, Card.TargetsTroops, true);
                    else
                        attackTarget.Health.TakeDamage(Card.damage);

                    if (assets != null) Fx.Spawn(assets.hitFx, attackTarget.AimPoint, 0.55f);
                    AudioManager.Play(string.IsNullOrEmpty(Card.impactSfx) ? Sfx.Hit : Card.impactSfx);
                    break;

                case AttackKind.Projectile:
                    if (attackTarget == null || !attackTarget.IsAlive || Card.projectilePrefab == null) return;

                    var offset = new Vector2(body.flipX ? -Card.projectileOrigin.x : Card.projectileOrigin.x, Card.projectileOrigin.y) * scale;
                    var projectile = Instantiate(Card.projectilePrefab, Position + offset, Quaternion.identity);
                    projectile.Launch(team, attackTarget, Card.damage, Card.splashRadius, Card.TargetsTroops);
                    if (!string.IsNullOrEmpty(Card.attackSfx)) AudioManager.Play(Card.attackSfx);
                    break;

                case AttackKind.Kamikaze:
                    Explode();
                    break;
            }
        }

        private void Explode()
        {
            Combat.DamageArea(Position, Mathf.Max(0.4f, Card.splashRadius), Card.damage, team, true, true);
            if (assets != null) Fx.Spawn(assets.explosionFx, Position + new Vector2(0f, 0.2f), 1.15f);
            AudioManager.Play(Sfx.Explosion);
            CameraRig.Shake(0.08f, 0.2f);
            Die(false);
        }

        private void OnDied() => Die(true);

        private void Die(bool spawnDeathFx)
        {
            if (state == State.Dead) return;
            state = State.Dead;
            attacking = false;
            desiredVelocity = Vector2.zero;

            if (circle != null) circle.enabled = false;
            if (rb != null) rb.simulated = false;
            healthBar.Hide();

            if (spawnDeathFx)
            {
                if (assets != null) Fx.Spawn(assets.deathFx, Position + new Vector2(0f, 0.15f), Mathf.Max(0.8f, scale));
                AudioManager.Play(Sfx.UnitDeath);
            }

            BattleManager.Instance?.NotifyUnitDied(this);
            Destroy(gameObject);
        }

        private void Face(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > 0.02f) body.flipX = direction.x < 0f;
        }

        private void SetAlpha(float alpha)
        {
            var c = body.color;
            c.a = alpha;
            body.color = c;
        }
    }
}
