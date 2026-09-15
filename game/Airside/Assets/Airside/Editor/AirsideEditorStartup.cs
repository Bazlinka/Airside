using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Airside.Editor
{
    [InitializeOnLoad]
    public static class AirsideEditorStartup
    {
        private const string PrototypeScene = "Assets/Airside/Scenes/AirsidePrototype.unity";

        static AirsideEditorStartup()
        {
            EditorApplication.delayCall += OpenPrototypeWhenNoSceneIsLoaded;
        }

        private const string OpenedThisSessionKey = "Airside.EditorStartup.OpenedPrototype";

        private static void OpenPrototypeWhenNoSceneIsLoaded()
        {
            // [InitializeOnLoad] runs after every domain reload, not just at launch. Opening the
            // prototype whenever the active scene had no path replaced a new, unsaved Untitled
            // scene on the next script recompile, and reloaded the scene in batch test runs.
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(OpenedThisSessionKey, false))
                return;
            SessionState.SetBool(OpenedThisSessionKey, true);

            var activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path) && !activeScene.isDirty)
                EditorSceneManager.OpenScene(PrototypeScene);
        }
    }
}
