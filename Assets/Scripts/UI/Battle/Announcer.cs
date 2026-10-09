using System.Collections;
using TMPro;
using UnityEngine;

namespace CrashRoyale.UI
{
    /// <summary>Big center-screen callouts: "BATTLE!", "2x ELIXIR!", "OVERTIME!".</summary>
    public class Announcer : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text subLabel;
        [SerializeField] private CanvasGroup group;

        private Coroutine routine;

        private void Awake() => group.alpha = 0f;

        public void Show(string text, Color color, string subText = null, float hold = 1.1f)
        {
            label.text = text;
            label.color = color;
            if (subLabel != null) subLabel.text = subText ?? string.Empty;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run(hold));
        }

        private IEnumerator Run(float hold)
        {
            UITween.Pop(transform, 0.4f, 1.15f, 0.32f);
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / 0.12f;
                yield return null;
            }

            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);

            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / 0.35f;
                yield return null;
            }

            group.alpha = 0f;
            routine = null;
        }
    }
}
