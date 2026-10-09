using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>One side's elixir. The player and the AI each own one and follow exactly the same rules.</summary>
    public class ElixirBank
    {
        public const float Max = 10f;

        public ElixirBank(float start, float regenPerSecond)
        {
            Current = Mathf.Clamp(start, 0f, Max);
            RegenPerSecond = regenPerSecond;
        }

        public float Current { get; private set; }
        public float RegenPerSecond { get; }
        public int Whole => Mathf.FloorToInt(Current + 0.0001f);
        public bool IsFull => Current >= Max - 0.0001f;

        /// <summary>Elixir that was lost while the bank was full (useful for AI tuning and stats).</summary>
        public float Wasted { get; private set; }

        public void Tick(float deltaTime, float multiplier)
        {
            float gain = RegenPerSecond * multiplier * deltaTime;
            float next = Current + gain;
            if (next > Max)
            {
                Wasted += next - Max;
                next = Max;
            }

            Current = next;
        }

        public bool CanAfford(int cost) => Current + 0.0001f >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Current = Mathf.Max(0f, Current - cost);
            return true;
        }

        internal void DebugSet(float value) => Current = Mathf.Clamp(value, 0f, Max);

        /// <summary>Seconds until the bank holds <paramref name="cost"/> elixir at the given multiplier.</summary>
        public float SecondsUntil(int cost, float multiplier)
        {
            if (CanAfford(cost)) return 0f;
            return (cost - Current) / Mathf.Max(0.0001f, RegenPerSecond * multiplier);
        }
    }
}
