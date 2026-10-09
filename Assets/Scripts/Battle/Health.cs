using System;
using UnityEngine;

namespace CrashRoyale.Battle
{
    public class Health : MonoBehaviour
    {
        private static readonly Color FlashColor = new Color(1f, 0.45f, 0.45f, 1f);
        private const float FlashTime = 0.09f;

        [SerializeField] private SpriteRenderer flashTarget;

        private float flashTimer;
        private Color baseColor = Color.white;

        public int Max { get; private set; } = 1;
        public int Current { get; private set; } = 1;
        public bool IsDead { get; private set; }
        public float Fraction => Max > 0 ? (float)Current / Max : 0f;

        public event Action<int> Damaged;
        public event Action Died;

        public void Init(int max)
        {
            Max = Mathf.Max(1, max);
            Current = Max;
            IsDead = false;
        }

        public void SetFlashTarget(SpriteRenderer target)
        {
            flashTarget = target;
            if (target != null) baseColor = target.color;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            Current = Mathf.Max(0, Current - amount);
            flashTimer = FlashTime;
            Damaged?.Invoke(amount);

            if (Current == 0)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        private void Update()
        {
            if (flashTarget == null) return;

            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                flashTarget.color = flashTimer > 0f ? FlashColor * baseColor : baseColor;
            }
        }
    }
}
