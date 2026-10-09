using System.Collections;
using CrashRoyale.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class EmoteBubble : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text text;

        private Coroutine routine;

        private void Awake() => group.alpha = 0f;

        public void Show(EmoteData emote)
        {
            bool hasIcon = emote.icon != null;
            icon.gameObject.SetActive(hasIcon);
            text.gameObject.SetActive(!hasIcon);
            if (hasIcon) icon.sprite = emote.icon;
            else text.text = emote.text;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            group.alpha = 1f;
            UITween.Pop(transform, 0.3f, 1.15f, 0.3f);
            yield return new WaitForSecondsRealtime(2.2f);

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
