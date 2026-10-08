using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Airside.Editor
{
    /// <summary>Compile/import the generated universal plugin before Unity packages and signs a Mac player.</summary>
    public sealed class MacNotificationsBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneOSX) return;
            if (Application.platform != RuntimePlatform.OSXEditor)
                throw new BuildFailedException("Airside's Mac notifications require a Mac build host with Xcode command-line tools.");
            var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            const string asset = "Assets/Plugins/macOS/AirsideNotifications.bundle";
            var destination = Path.Combine(Application.dataPath, "Plugins/macOS/AirsideNotifications.bundle");
            var staging = Path.Combine(Application.temporaryCachePath, "AirsideNotifications.bundle");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            var script = Path.Combine(repo, "scripts/build-mac-notifications.sh");
            var start = new ProcessStartInfo("/bin/bash", Quote(script) + " " + Quote(staging))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not start the notification bridge compiler.");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    throw new BuildFailedException("Notification bridge compilation exceeded 60 seconds.");
                }
                if (process.ExitCode != 0)
                    throw new BuildFailedException("Notification bridge compilation failed: " + error.GetAwaiter().GetResult());
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            FileUtil.CopyFileOrDirectory(staging, destination);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(asset) as PluginImporter;
            if (importer == null) throw new BuildFailedException("Unity did not import the notification bundle as a native plugin.");
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
            importer.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
            importer.SaveAndReimport();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneOSX) return;
            var plugins = Path.Combine(report.summary.outputPath, "Contents/Plugins");
            if (!Directory.Exists(plugins) ||
                Directory.GetFiles(plugins, "AirsideNotifications", SearchOption.AllDirectories).Length == 0)
                throw new BuildFailedException("The built Airside.app is missing its notification bridge. Rebuild on a Mac; do not distribute this player.");
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
