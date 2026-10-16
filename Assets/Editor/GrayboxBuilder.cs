using System.IO;
using UnityEditor;
using UnityEngine;

public static class GrayboxBuilder
{
    private const float FloorSize = 100f;
    private const float FloorThickness = 1f;
    private const string MaterialPath = "Assets/Materials/Graybox_Grid.mat";
    private const string GridTexturePath = "Assets/Textures/Prototype/general/prototype_grid_grey.png";

    [MenuItem("A Quick Sprint Through Hell/Build Graybox Floor")]
    public static void BuildFloor()
    {
        var root = GameObject.Find("Graybox");
        if (root == null)
        {
            root = new GameObject("Graybox");
            Undo.RegisterCreatedObjectUndo(root, "Create Graybox Root");
        }

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Graybox_Floor";
        Undo.RegisterCreatedObjectUndo(floor, "Create Graybox Floor");

        floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(0f, -FloorThickness * 0.5f, 0f);
        floor.transform.localScale = new Vector3(FloorSize, FloorThickness, FloorSize);

        var renderer = floor.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateGridMaterial();

        // Cube primitives already get a BoxCollider, which is what you want to walk/roll on.
        floor.isStatic = true; // treat as non-moving level geometry (batching, baked lighting, navmesh)

        Selection.activeGameObject = floor;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log($"Graybox floor built: {FloorSize}m x {FloorSize}m, top surface at y=0. " +
                  "Grid tiling is set to 1 repeat/meter as a starting point -- tweak the material's Tiling X/Y " +
                  "in the Inspector if the squares look too big or small. " +
                  "Heads up: the scene still has a leftover 'Plane' and 'Cube' test object from the default template " +
                  "that you may want to delete or move.");
    }

    private static Material GetOrCreateGridMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
            return existing;

        var dir = Path.GetDirectoryName(MaterialPath);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = "Graybox_Grid" };

        var gridTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        if (gridTexture != null)
        {
            mat.SetTexture("_BaseMap", gridTexture);
            mat.SetTextureScale("_BaseMap", new Vector2(FloorSize, FloorSize));
        }
        else
        {
            mat.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.55f));
        }

        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.2f);

        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
