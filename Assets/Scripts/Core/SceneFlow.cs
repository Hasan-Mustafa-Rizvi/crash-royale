using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrashRoyale.Core
{
    /// <summary>Scene names and transitions. Every load resets the time scale so a paused battle can never leak into a menu.</summary>
    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string Deck = "Deck";
        public const string Shop = "Shop";
        public const string Battle = "Battle";

        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneFader.FadeTo(sceneName);
        }
    }

    /// <summary>Persistent full-screen fade used between scenes.</summary>
    public sealed class SceneFader : MonoBehaviour
    {
        private const float FadeDuration = 0.18f;

        private static SceneFader instance;
        private CanvasGroup group;
        private bool busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;

            var go = new GameObject("SceneFader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneFader>();

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            go.AddComponent<GraphicRaycaster>();

            instance.group = go.AddComponent<CanvasGroup>();
            instance.group.alpha = 0f;
            instance.group.blocksRaycasts = false;

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(go.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = new Color(0.06f, 0.03f, 0.08f, 1f);
        }

        public static void FadeTo(string sceneName)
        {
            if (instance == null || instance.busy || !Application.isPlaying)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            instance.StartCoroutine(instance.Run(sceneName));
        }

        private IEnumerator Run(string sceneName)
        {
            busy = true;
            group.blocksRaycasts = true;
            yield return Fade(0f, 1f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;

            yield return Fade(1f, 0f);
            group.blocksRaycasts = false;
            busy = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < FadeDuration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / FadeDuration);
                yield return null;
            }

            group.alpha = to;
        }
    }
}
