using UnityEditor;
using UnityEngine;

// puts the lobby (CustomPlay) first in Build Settings, then TheFactory, so a built game opens in the lobby and the portal can load the factory
// safe to run again.
// note that pressing Play in the editor always starts the scene that is open
public static class BuildScenesSetup
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/CustomPlay.unity",
        "Assets/Scenes/TheFactory.unity"
    };

    [MenuItem("A Quick Sprint Through Hell/Set Up Build Scenes")]
    public static void SetUp()
    {
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[ScenePaths.Length];

        for (int i = 0; i < ScenePaths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePaths[i]) == null)
            {
                Debug.LogError($"Scene not found: {ScenePaths[i]}");
                return;
            }

            scenes[i] = new EditorBuildSettingsScene(ScenePaths[i], true);
        }

        EditorBuildSettings.scenes = scenes;

        Debug.Log("Build Settings now has CustomPlay (first, the lobby) and TheFactory (second). Any other scenes were removed from the list.");
    }
}
