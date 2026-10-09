using System.Collections;
using UnityEngine;

namespace CrashRoyale.UI
{
    /// <summary>Tiny unscaled-time tweens for UI juice (pops and fades). Works while the game is paused.</summary>
    public sealed class UITween : MonoBehaviour
    {
        private static UITween runner;

        private static UITween Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("UITween");
                    DontDestroyOnLoad(go);
                    runner = go.AddComponent<UITween>();
                }

                return runner;
            }
        }

        public static Coroutine Pop(Transform target, float from = 0.6f, float overshoot = 1.12f, float duration = 0.28f)
        {
            if (target == null || !Application.isPlaying) return null;
            return Runner.StartCoroutine(PopRoutine(target, from, overshoot, duration));
        }

        public static Coroutine Punch(Transform target, float amount = 0.15f, float duration = 0.22f)
        {
            if (target == null || !Application.isPlaying) return null;
            return Runner.StartCoroutine(PunchRoutine(target, amount, duration));
        }

        public static Coroutine Fade(CanvasGroup group, float to, float duration)
        {
            if (group == null || !Application.isPlaying) return null;
            return Runner.StartCoroutine(FadeRoutine(group, to, duration));
        }

        private static IEnumerator PopRoutine(Transform target, float from, float overshoot, float duration)
        {
            float half = duration * 0.6f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                float s = t < half
                    ? Mathf.Lerp(from, overshoot, EaseOut(t / half))
                    : Mathf.Lerp(overshoot, 1f, EaseOut((t - half) / (duration - half)));
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            if (target != null) target.localScale = Vector3.one;
        }

        private static IEnumerator PunchRoutine(Transform target, float amount, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                float s = 1f + Mathf.Sin(t / duration * Mathf.PI) * amount;
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            if (target != null) target.localScale = Vector3.one;
        }

        private static IEnumerator FadeRoutine(CanvasGroup group, float to, float duration)
        {
            float from = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (group == null) yield break;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            if (group != null) group.alpha = to;
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
    }
}
