#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CrashRoyale.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CrashRoyale.Dev
{
    /// <summary>Small helpers shared by the dev runs: waiting, clicking real buttons, finding towers, rendering screenshots.</summary>
    internal static class DevUtil
    {
        public static string ActiveScene => SceneManager.GetActiveScene().name;

        public static IEnumerator WaitForScene(string scene, float timeout = 15f)
        {
            float limit = Time.realtimeSinceStartup + timeout;
            while (ActiveScene != scene && Time.realtimeSinceStartup < limit) yield return null;

            // Let Start() run and the fade finish.
            yield return new WaitForSecondsRealtime(0.5f);
        }

        public static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float limit = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < limit) yield return null;
        }

        /// <summary>Reloading Battle keeps the scene name, so wait for a different BattleManager to finish starting.</summary>
        public static IEnumerator WaitForNewBattle(BattleManager previous, float timeout = 15f)
        {
            yield return WaitFor(() => IsNewBattle(previous), timeout);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        public static bool IsNewBattle(BattleManager previous)
        {
            var current = BattleManager.Instance;
            return current != null && current != previous && current.Deck(Team.Blue) != null;
        }

        public static Button FindButton(string name)
        {
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (button.name == name && button.isActiveAndEnabled && button.interactable) return button;
            }

            return null;
        }

        /// <summary>Presses a visible, interactable button by name, exactly like a tap would.</summary>
        public static bool Click(string name)
        {
            var button = FindButton(name);
            if (button == null) return false;
            button.onClick.Invoke();
            return true;
        }

        public static bool IsActive(string gameObjectName) => GameObject.Find(gameObjectName) != null;

        public static int Count<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        public static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        public static Tower FindTower(BattleManager battle, Team team, bool king, int lane = -1)
        {
            foreach (var tower in battle.Towers)
            {
                if (tower != null && tower.Team == team && tower.IsKing == king && (king || tower.Lane == lane)) return tower;
            }

            return null;
        }

        public static int AliveTowers(Team team)
        {
            var towers = new List<Tower>();
            BattleRegistry.GetTowers(team, towers);
            return towers.Count;
        }

        // ---------------------------------------------------------------- screenshots

        /// <summary>
        /// Renders the main camera and every overlay canvas into a PNG. Overlay canvases never go through a camera, so they
        /// are switched to camera space for the capture and switched back afterwards.
        /// </summary>
        public static IEnumerator Capture(string path, int width = 1080, int height = 1920)
        {
            var session = CaptureSession.Begin(width, height);
            if (session == null) yield break;

            float timeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;
            yield return null;

            session.Save(path);
            session.End();
            Time.timeScale = timeScale;
        }

        /// <summary>Records a run of frames while the game keeps playing in fixed steps of <paramref name="step"/> seconds.</summary>
        public static IEnumerator CaptureSequence(string folder, int frames, float step, int width, int height)
        {
            Directory.CreateDirectory(folder);
            var session = CaptureSession.Begin(width, height);
            if (session == null) yield break;

            Time.captureDeltaTime = step;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                session.Save(Path.Combine(folder, $"frame_{i:000}.png"));
            }

            Time.captureDeltaTime = 0f;
            session.End();
        }

        private sealed class CaptureSession
        {
            private readonly List<(Canvas canvas, int order)> canvases = new List<(Canvas, int)>();
            private Camera camera;
            private RenderTexture target;
            private Texture2D readback;

            public static CaptureSession Begin(int width, int height)
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    Debug.LogWarning("[Dev] No main camera to capture");
                    return null;
                }

                var session = new CaptureSession { camera = camera };
                session.target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                session.target.Create();
                session.readback = new Texture2D(width, height, TextureFormat.RGB24, false);
                camera.targetTexture = session.target;

                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.name == "SceneFader") continue;
                    session.canvases.Add((canvas, canvas.sortingOrder));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                    canvas.sortingOrder = 20000 + canvas.sortingOrder;
                }

                return session;
            }

            public void Save(string path)
            {
                if (camera == null) return;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }

            public void End()
            {
                if (camera != null) camera.targetTexture = null;
                foreach (var (canvas, order) in canvases)
                {
                    if (canvas == null) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = order;
                }

                Object.Destroy(readback);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
#endif
