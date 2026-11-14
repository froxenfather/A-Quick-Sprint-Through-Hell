using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

// a window with two material boxes and a button: every renderer that uses the first material gets the second one instead.
// tick Prefabs, Scenes or both to choose where to look.
// menu: A Quick Sprint Through Hell > Replace Materials
public class ReplaceMaterialsWindow : EditorWindow
{
    private Material from;
    private Material to;
    private bool includePrefabs = true;
    private bool includeScenes = true;

    [MenuItem("A Quick Sprint Through Hell/Replace Materials")]
    private static void Open()
    {
        GetWindow<ReplaceMaterialsWindow>("Replace Materials");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Every renderer that uses the first material gets the second one. This edits and saves your prefabs and scenes, and Ctrl+Z can't undo it, so make a backup first.", MessageType.Info);

        from = (Material)EditorGUILayout.ObjectField("Replace this material", from, typeof(Material), false);
        to = (Material)EditorGUILayout.ObjectField("With this material", to, typeof(Material), false);

        // ProBuilder's own default lives inside a package, so it is hard to find to drag in
        if (GUILayout.Button("Fill the first box with ProBuilder's default material"))
            from = BuiltinMaterials.defaultMaterial;

        EditorGUILayout.Space();

        includePrefabs = EditorGUILayout.Toggle("Prefabs", includePrefabs);
        includeScenes = EditorGUILayout.Toggle("Scenes", includeScenes);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(from == null || to == null || from == to || (!includePrefabs && !includeScenes)))
        {
            if (GUILayout.Button("Replace", GUILayout.Height(30)))
                Replace();
        }
    }

    private void Replace()
    {
        string where = includePrefabs && includeScenes ? "every prefab and scene" : (includePrefabs ? "every prefab" : "every scene");

        if (!EditorUtility.DisplayDialog("Replace materials", $"Replace '{from.name}' with '{to.name}' in {where}?\nThis edits and saves your files.", "Replace", "Cancel"))
            return;

        // opening scenes closes the current one, so save it first
        if (includeScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int prefabsChanged = 0;
        int scenesChanged = 0;
        string fromPath = AssetDatabase.GetAssetPath(from);
        string originalScene = SceneManager.GetActiveScene().path;

        try
        {
            if (includePrefabs)
                prefabsChanged = ReplaceInPrefabs(fromPath);

            if (includeScenes)
                scenesChanged = ReplaceInScenes(fromPath);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // back to the scene that was open
        if (includeScenes && !string.IsNullOrEmpty(originalScene))
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();

        Debug.Log($"Replaced '{from.name}' with '{to.name}': {prefabsChanged} prefabs and {scenesChanged} scenes changed.");
    }

    private int ReplaceInPrefabs(string fromPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int changedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar("Replace materials: prefabs", path, (float)i / guids.Length);

            if (!UsesMaterial(path, fromPath))
                continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                bool changed = false;

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    changed |= Swap(renderer);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedCount++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changedCount;
    }

    private int ReplaceInScenes(string fromPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        int changedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar("Replace materials: scenes", path, (float)i / guids.Length);

            if (!UsesMaterial(path, fromPath))
                continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;

            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= Swap(renderer);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changedCount++;
            }
        }

        return changedCount;
    }

    // a quick check so files that can't use the material are skipped without opening them.
    // a material with no file path (a built-in one) can't be checked this way, so every file is opened
    private static bool UsesMaterial(string assetPath, string materialPath)
    {
        if (string.IsNullOrEmpty(materialPath))
            return true;

        return Array.IndexOf(AssetDatabase.GetDependencies(assetPath, false), materialPath) >= 0;
    }

    // gives the renderer the new material in every slot that had the old one, true if anything changed
    private bool Swap(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == from)
            {
                materials[i] = to;
                changed = true;
            }
        }

        if (changed)
            renderer.sharedMaterials = materials;

        return changed;
    }
}
