using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>Anything that can be targeted and damaged: troops and towers.</summary>
    [RequireComponent(typeof(Health))]
    public abstract class Combatant : MonoBehaviour
    {
        [SerializeField] protected Team team;
        [SerializeField] protected float radius = 0.3f;

        private Health health;

        public Team Team => team;
        public float Radius => radius;
        public Health Health => health != null ? health : health = GetComponent<Health>();
        public abstract bool IsBuilding { get; }
        public bool IsAlive => this != null && Health != null && !Health.IsDead;
        public Vector2 Position => transform.position;

        /// <summary>Where projectiles should aim (the body, not the feet).</summary>
        public virtual Vector2 AimPoint => Position + new Vector2(0f, 0.3f);

        protected virtual void OnEnable() => BattleRegistry.Add(this);

        protected virtual void OnDisable() => BattleRegistry.Remove(this);

        public float EdgeDistanceTo(Combatant other)
        {
            return Vector2.Distance(Position, other.Position) - other.Radius - radius;
        }
    }
}
