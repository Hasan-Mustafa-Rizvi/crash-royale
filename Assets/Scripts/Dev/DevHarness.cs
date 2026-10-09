#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CrashRoyale.Battle;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using UnityEngine;

namespace CrashRoyale.Dev
{
    /// <summary>
    /// Self-tests built into desktop builds. Nothing here runs unless the game is started with one of these flags:
    /// <code>
    ///   -crFlow            clicks through menu, name prompt, shop, deck, battle, pause and results like a player would
    ///   -crSim [games]     AI vs AI tournament, every difficulty pairing (default 4 games each)
    ///   -crMirror [seeds]  side check: the same two decks played from both sides (default 20 seeds)
    ///   -crShots           renders every screen to PNG (needs a graphics device, so no -nographics)
    ///   -crOut [dir]       where results go (default: persistentDataPath/dev)
    ///   -crTune a.b=v,...  override AI tuning or card stats for this run, e.g. Hard.Tolerance=150,kaboom.damage=140
    /// </code>
    /// Example: <c>CrashRoyale.exe -batchmode -nographics -crSim 10 -crOut results -logFile results/player.log</c>
    /// A dev run uses a throwaway in-memory profile, never the real save, and quits when done.
    /// Exit code 0 means every check passed and nothing logged an error.
    /// </summary>
    public sealed class DevHarness : MonoBehaviour
    {
        private const int MaxStoredMessages = 60;
        private const float WatchdogSeconds = 1800f;

        private readonly object gate = new object();
        private readonly List<string> errorMessages = new List<string>();
        private readonly List<string> warningMessages = new List<string>();
        private readonly HashSet<string> seenMessages = new HashSet<string>();
        private string progressPath;
        private int errorCount;
        private int warningCount;
        private int checks;
        private int failures;
        private bool finished;

        public static DevHarness Instance { get; private set; }
        public string OutputDir { get; private set; }
        public int Failures => failures;

        public int ErrorCount
        {
            get
            {
                lock (gate) return errorCount;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor) return;

            string[] args = Environment.GetCommandLineArgs();
            bool flow = HasFlag(args, "-crFlow");
            bool sim = HasFlag(args, "-crSim") || HasFlag(args, "-crMirror");
            bool shots = HasFlag(args, "-crShots");
            if (!flow && !sim && !shots) return;

            var go = new GameObject("DevHarness");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DevHarness>();
            Instance.Begin(args, flow, sim, shots);
        }

        private void Begin(string[] args, bool flow, bool sim, bool shots)
        {
            OutputDir = StringArg(args, "-crOut") ?? Path.Combine(Application.persistentDataPath, "dev");
            Directory.CreateDirectory(OutputDir);
            progressPath = Path.Combine(OutputDir, "progress.txt");
            File.WriteAllText(progressPath, $"Crash Royale {Application.version} dev run, {DateTime.Now:yyyy-MM-dd HH:mm:ss}, graphics: {SystemInfo.graphicsDeviceType}\n");

            // A throwaway save, so a dev run never touches real progress.
            PlayerProfile.Store = new MemoryStore();
            Application.runInBackground = true;
            Application.logMessageReceivedThreaded += OnLog;

            ApplyOverrides(StringArg(args, "-crTune"));
            StartCoroutine(Run(args, flow, sim, shots));
        }

        /// <summary>Sets AI tuning fields (Easy/Normal/Hard) or card fields by name, for balance experiments.</summary>
        private void ApplyOverrides(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return;

            const BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            foreach (var part in spec.Split(','))
            {
                var pair = part.Split('=');
                var path = pair[0].Split('.');
                if (pair.Length != 2 || path.Length != 2)
                {
                    Report($"tune: can't read '{part}'");
                    continue;
                }

                var preset = typeof(AIController).GetField(path[0], BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                object target = preset != null ? preset.GetValue(null) : CardDatabase.Load().Get(path[0]);
                var field = target?.GetType().GetField(path[1], fieldFlags);
                if (field == null)
                {
                    Report($"tune: nothing called '{pair[0]}'");
                    continue;
                }

                object value = Convert.ChangeType(pair[1], field.FieldType, CultureInfo.InvariantCulture);
                field.SetValue(target, value);
                Report($"tune: {path[0]}.{field.Name} = {value}");
            }
        }

        private IEnumerator Run(string[] args, bool flow, bool sim, bool shots)
        {
            // Let the first scene finish loading.
            yield return null;
            yield return null;

            if (flow) yield return Guard(new FlowCheck(this).Run(), "flow");
            if (HasFlag(args, "-crSim")) yield return Guard(new AiTournament(this, IntArg(args, "-crSim", 4), FloatArg(args, "-crStep", 0.02f)).Run(), "tournament");
            if (HasFlag(args, "-crMirror")) yield return Guard(new AiTournament(this, 1, FloatArg(args, "-crStep", 0.02f)).RunMirror(IntArg(args, "-crMirror", 20), Battle.Difficulty.Normal), "mirror");
            if (shots) yield return Guard(new ScreenTour(this).Run(), "screenshots");

            Finish();
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup > WatchdogSeconds)
            {
                Fail("watchdog: the dev run took too long");
                Finish();
            }
        }

        // ---------------------------------------------------------------- reporting

        public void Report(string line)
        {
            Debug.Log("[Dev] " + line);
            File.AppendAllText(progressPath, line + "\n");
        }

        public bool Check(bool condition, string what)
        {
            checks++;
            if (condition)
            {
                Report("  ok    " + what);
            }
            else
            {
                failures++;
                Report("  FAIL  " + what);
            }

            return condition;
        }

        public void Fail(string what)
        {
            failures++;
            Report("  FAIL  " + what);
        }

        public void WriteFile(string name, string text) => File.WriteAllText(Path.Combine(OutputDir, name), text);

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;

            var sb = new StringBuilder();
            lock (gate)
            {
                sb.AppendLine($"errors: {errorCount}, warnings: {warningCount} (unique messages below)");
                sb.AppendLine();
                foreach (var line in errorMessages) sb.AppendLine(line);
                sb.AppendLine();
                foreach (var line in warningMessages) sb.AppendLine(line);
            }

            WriteFile("log_messages.txt", sb.ToString());
            Report($"DONE checks={checks} failures={failures} errors={ErrorCount} warnings={warningCount}");
            Application.logMessageReceivedThreaded -= OnLog;
            Application.Quit(failures > 0 || ErrorCount > 0 ? 1 : 0);
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log) return;

            lock (gate)
            {
                bool isError = type != LogType.Warning;
                if (isError) errorCount++;
                else warningCount++;

                if (!seenMessages.Add(type + message)) return;
                var list = isError ? errorMessages : warningMessages;
                if (list.Count >= MaxStoredMessages) return;

                string trace = string.Empty;
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    var lines = stackTrace.Split('\n');
                    trace = string.Join(" | ", lines, 0, Math.Min(4, lines.Length)).Trim();
                }

                list.Add($"[{type}] {message}" + (trace.Length > 0 ? "\n    " + trace : string.Empty));
            }
        }

        /// <summary>
        /// Runs a step and turns an exception into a failed check instead of a silently dead coroutine.
        /// Nested enumerators are flattened so exceptions anywhere inside are caught too.
        /// </summary>
        private IEnumerator Guard(IEnumerator routine, string name)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                object current;
                try
                {
                    if (!top.MoveNext())
                    {
                        stack.Pop();
                        continue;
                    }

                    current = top.Current;
                }
                catch (Exception e)
                {
                    Time.captureDeltaTime = 0f;
                    Fail($"{name} threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                    yield break;
                }

                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return current;
            }
        }

        // ---------------------------------------------------------------- arguments

        private static int IndexOf(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        private static bool HasFlag(string[] args, string flag) => IndexOf(args, flag) >= 0;

        private static string StringArg(string[] args, string flag)
        {
            int i = IndexOf(args, flag);
            if (i < 0 || i + 1 >= args.Length || args[i + 1].StartsWith("-")) return null;
            return args[i + 1];
        }

        private static int IntArg(string[] args, string flag, int fallback)
        {
            return int.TryParse(StringArg(args, flag), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        private static float FloatArg(string[] args, string flag, float fallback)
        {
            return float.TryParse(StringArg(args, flag), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }
    }
}
#endif
