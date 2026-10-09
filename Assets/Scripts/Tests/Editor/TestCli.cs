using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CrashRoyale.Tests
{
    /// <summary>
    /// Runs the test suites headless through the TestRunner API and writes a plain-text report.
    /// Usage: Unity -batchmode -projectPath . -executeMethod CrashRoyale.Tests.TestCli.RunPlayMode [-crTestFilter Name] [-crReport path]
    /// </summary>
    [InitializeOnLoad]
    public static class TestCli
    {
        private const string ActiveKey = "cr.testcli.active";
        private const string ReportKey = "cr.testcli.report";

        static TestCli()
        {
            // Callbacks have to be registered again after every domain reload (entering play mode reloads it).
            if (!SessionState.GetBool(ActiveKey, false)) return;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Reporter());
        }

        public static void RunEditMode() => Run(TestMode.EditMode);

        public static void RunPlayMode() => Run(TestMode.PlayMode);

        private static void Run(TestMode mode)
        {
            string report = Arg("-crReport") ?? $"Logs/test-report-{mode}.txt";
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(ReportKey, report);
            File.WriteAllText(report, $"RUNNING {mode} {DateTime.Now:o}\n");

            var filter = new Filter { testMode = mode };
            string names = Arg("-crTestFilter");
            if (!string.IsNullOrEmpty(names)) filter.groupNames = names.Split(';');

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Reporter());
            api.Execute(new ExecutionSettings(filter) { runSynchronously = mode == TestMode.EditMode });
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private class Reporter : ICallbacks
        {
            private readonly StringBuilder lines = new StringBuilder();

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                string line = $"{result.TestStatus,-12} {result.Duration,7:0.0}s  {result.FullName}";
                if (result.TestStatus == TestStatus.Failed)
                {
                    line += $"\n    {result.Message?.Trim()}\n    {FirstLines(result.StackTrace, 4)}";
                }

                File.AppendAllText(SessionState.GetString(ReportKey, "Logs/test-report.txt"), line + "\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string report = SessionState.GetString(ReportKey, "Logs/test-report.txt");
                File.AppendAllText(report, $"DONE passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount}\n");
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(result.FailCount > 0 ? 2 : 0);
            }

            private static string FirstLines(string text, int count)
            {
                if (string.IsNullOrEmpty(text)) return string.Empty;
                return string.Join("\n    ", text.Split('\n').Take(count).Select(l => l.Trim()));
            }
        }
    }
}
