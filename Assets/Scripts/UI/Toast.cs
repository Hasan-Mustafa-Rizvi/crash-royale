using System.Collections;
using TMPro;
using UnityEngine;

namespace CrashRoyale.UI
{
    /// <summary>Short message that fades in and out ("Not enough elixir", "Unlocked Archers!").</summary>
    public class Toast : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;

        private static Toast instance;
        private Coroutine routine;

        private void Awake()
        {
            instance = this;
            group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Show(string message, float duration = 1.5f)
        {
            if (instance != null) instance.Display(message, duration);
        }

        private void Display(string message, float duration)
        {
            label.text = message;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run(duration));
        }

        private IEnumerator Run(float duration)
        {
            UITween.Pop(transform, 0.85f, 1.05f, 0.2f);
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / 0.12f;
                yield return null;
            }

            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(duration);

            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / 0.3f;
                yield return null;
            }

            group.alpha = 0f;
            routine = null;
        }
    }
}
