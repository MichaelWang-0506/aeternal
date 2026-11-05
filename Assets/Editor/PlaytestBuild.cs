using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Aeternal.EditorTools
{
    /// <summary>
    /// Command-line build for the automated playtest (Assets/Scripts/Debug/PlaytestDriver.cs):
    /// a macOS development build of the scenes in Build Settings, under the product name
    /// "Aeternal Playtest" so its save file and settings stay apart from the real game's.
    /// Run: Unity -batchmode -quit -projectPath &lt;copy&gt; -executeMethod
    /// Aeternal.EditorTools.PlaytestBuild.Build -playtestOut &lt;path&gt;.app
    /// Meant for a copy of the project: it changes the product name in PlayerSettings.
    /// </summary>
    public static class PlaytestBuild
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-playtestOut");
            string output = i >= 0 && i < args.Length - 1 ? args[i + 1] : "Builds/Playtest/Aeternal Playtest.app";

            PlayerSettings.productName = "Aeternal Playtest";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[PlaytestBuild] {report.summary.result}: {output} ({report.summary.totalErrors} errors)");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
