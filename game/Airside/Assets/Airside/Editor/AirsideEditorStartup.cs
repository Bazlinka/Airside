using UnityEditor;
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

        private static void OpenPrototypeWhenNoSceneIsLoaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
                EditorSceneManager.OpenScene(PrototypeScene);
        }
    }
}
