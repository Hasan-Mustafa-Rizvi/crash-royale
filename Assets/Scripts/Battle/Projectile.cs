using CrashRoyale.Audio;
using CrashRoyale.Visual;
using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>
    /// Arrows, dynamite and tower shots. Follows its target (or the target's last position if it dies) and
    /// deals single-target or splash damage on arrival. Lobbed projectiles fake height with a parabola.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 7f;
        [SerializeField] private float arcHeight;
        [SerializeField] private bool faceDirection = true;
        [SerializeField] private float spinDegreesPerSecond;
        [SerializeField] private GameObject impactFx;
        [SerializeField] private float impactFxScale = 1f;
        [SerializeField] private string impactSfx;

        private Team team;
        private Combatant target;
        private Vector2 start;
        private Vector2 targetPoint;
        private float duration;
        private float elapsed;
        private int damage;
        private float splashRadius;
        private bool splashHitsTroops = true;
        private bool launched;

        public void Launch(Team owner, Combatant targetCombatant, int hitDamage, float splash, bool splashTroops = true)
        {
            team = owner;
            target = targetCombatant;
            damage = hitDamage;
            splashRadius = splash;
            splashHitsTroops = splashTroops;
            start = transform.position;
            targetPoint = target != null ? (splashRadius > 0f ? target.Position : target.AimPoint) : start;
            duration = Mathf.Max(0.12f, Vector2.Distance(start, targetPoint) / Mathf.Max(0.1f, speed));
            launched = true;
            UpdateTransform(0f, Vector2.zero);
        }

        private Vector2 Aim()
        {
            if (target == null || !target.IsAlive) return targetPoint;
            return splashRadius > 0f ? target.Position : target.AimPoint;
        }

        private void Update()
        {
            if (!launched) return;

            if (target != null && target.IsAlive) targetPoint = Aim();

            Vector2 previous = transform.position;
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            UpdateTransform(u, previous);

            if (u >= 1f) Impact();
        }

        private void UpdateTransform(float u, Vector2 previous)
        {
            Vector2 ground = Vector2.Lerp(start, targetPoint, u);
            float height = arcHeight * 4f * u * (1f - u);
            Vector2 position = ground + new Vector2(0f, height);
            transform.position = position;

            if (spinDegreesPerSecond != 0f)
            {
                transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);
            }
            else if (faceDirection)
            {
                Vector2 dir = u <= 0f ? targetPoint - start : position - previous;
                if (dir.sqrMagnitude > 0.000001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
        }

        private void Impact()
        {
            launched = false;

            if (splashRadius > 0f)
            {
                Combat.DamageArea(targetPoint, splashRadius, damage, team, splashHitsTroops, true);
            }
            else if (target != null && target.IsAlive)
            {
                target.Health.TakeDamage(damage);
            }

            Fx.Spawn(impactFx, targetPoint, impactFxScale);
            if (!string.IsNullOrEmpty(impactSfx)) AudioManager.Play(impactSfx);
            Destroy(gameObject);
        }
    }
}
