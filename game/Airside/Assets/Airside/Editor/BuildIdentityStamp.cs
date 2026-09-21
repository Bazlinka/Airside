using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Airside.Editor
{
    /// <summary>
    /// Writes StreamingAssets/build-identity.txt from git so Play and a later Mac build
    /// show the commit that is actually open. Skips batch tests. Rewrites only when the
    /// commit, branch or dirty flag changed, except when entering Play, which refreshes
    /// the built time.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildIdentityStamp
    {
        static BuildIdentityStamp()
        {
            EditorApplication.delayCall += () => Run(refreshBuiltTime: false);
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                Run(refreshBuiltTime: true);
        }

        private static void Run(bool refreshBuiltTime)
        {
            if (Application.isBatchMode)
                return;

            try
            {
                var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                var script = Path.Combine(repo, "scripts", "stamp-build-identity.sh");
                if (!File.Exists(script))
                    return;

                var start = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = Quote(script) + (refreshBuiltTime ? " --refresh" : ""),
                    WorkingDirectory = repo,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var process = Process.Start(start))
                {
                    if (process == null)
                        return;
                    // Short script; read both pipes so a full buffer cannot stall Unity.
                    var stderr = process.StandardError.ReadToEnd();
                    process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(15000))
                    {
                        Debug.LogWarning("Airside build stamp timed out.");
                        return;
                    }

                    if (process.ExitCode != 0)
                        Debug.LogWarning("Airside build stamp failed: " + stderr.Trim());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Airside build stamp failed: " + ex.Message);
            }
        }

        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
    }
}
