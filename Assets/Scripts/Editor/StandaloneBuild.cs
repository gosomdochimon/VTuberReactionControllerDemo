using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VTuberReactionControllerDemo.Editor
{
    public static class StandaloneBuild
    {
        private const string DefaultBuildPath = "Builds/Windows/VTuberReactionControllerDemo.exe";
        private const int DemoWindowWidth = 960;
        private const int DemoWindowHeight = 540;

        [MenuItem("VTuber Demo/Build Windows Standalone")]
        public static void BuildWindowsStandalone()
        {
            string outputPath = GetBuildPathFromCommandLine();
            string[] scenes = GetEnabledBuildScenes();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured in EditorBuildSettings.");
            }

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            ConfigureDemoWindowSettings();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows Standalone build failed with result {summary.result}.");
            }

            UnityEngine.Debug.Log(
                $"Windows Standalone build succeeded: {summary.outputPath} ({summary.totalSize} bytes)");
        }

        private static void ConfigureDemoWindowSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = DemoWindowWidth;
            PlayerSettings.defaultScreenHeight = DemoWindowHeight;
            PlayerSettings.resizableWindow = true;
        }

        private static string GetBuildPathFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-buildPath", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return DefaultBuildPath;
        }

        private static string[] GetEnabledBuildScenes()
        {
            return Array.ConvertAll(
                Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
                scene => scene.path);
        }
    }
}
