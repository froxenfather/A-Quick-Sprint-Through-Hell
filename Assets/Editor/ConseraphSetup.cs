using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// builds Conseraph in the open scene, or brings the one already there up to date.
// he is a root with a trigger sphere, the Conseraph, ConseraphAudio and ConseraphVisuals scripts, and four sprite parts (halo, ring, face, mouth).
// the pictures are read from Assets/Conseraph as halo.png, ring.png, face.png and mouth.png, and the sound as toll
public static class ConseraphSetup
{
    private const string ArtFolder = "Assets/Conseraph";
    private const string HaloPath = ArtFolder + "/halo.png";
    private const string RingPath = ArtFolder + "/ring.png";
    private const string FacePath = ArtFolder + "/face.png";
    private const string MouthPath = ArtFolder + "/mouth.png";

    // the shader that draws him over every wall, and the material made from it
    private const string ShaderName = "Custom/Conseraph Sprite";
    private const string MaterialPath = "Assets/Materials/conseraph_sprite.mat";

    // the face's picture is scaled to this tall (in meters) on the root's default scale.
    // the halo, the ring and the mouth get the same pixels per meter, so they keep the sizes they have in the art
    private const float FaceHeightMeters = 2f;

    // the trigger that detects touching the player is a sphere about as big as the face
    private const float TouchRadius = 1f;

    // how far in front of the player he is put the first time, and how high above their feet
    private const float StartDistance = 8f;
    private const float StartHeight = 2f;

    [MenuItem("A Quick Sprint Through Hell/Set Up Conseraph")]
    public static void SetUp()
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Conseraph");
            Debug.LogWarning($"Made the folder {ArtFolder}. Put halo.png, ring.png, face.png, mouth.png and the toll sound in it, then run this again.");
        }

        Material material = LoadMaterial();

        if (material == null)
            return;

        // the face sets the scale of all four, so it is imported first
        Sprite faceSprite = LoadSprite(FacePath, -1f);
        float pixelsPerUnit = faceSprite != null ? faceSprite.rect.height / FaceHeightMeters : 100f;

        Sprite haloSprite = LoadSprite(HaloPath, pixelsPerUnit);
        Sprite ringSprite = LoadSprite(RingPath, pixelsPerUnit);
        Sprite mouthSprite = LoadSprite(MouthPath, pixelsPerUnit);

        if (faceSprite != null)
            faceSprite = LoadSprite(FacePath, pixelsPerUnit);

        WarnIfMissing(haloSprite, HaloPath);
        WarnIfMissing(ringSprite, RingPath);
        WarnIfMissing(faceSprite, FacePath);
        WarnIfMissing(mouthSprite, MouthPath);

        AudioClip tollClip = LoadToll();

        if (tollClip == null)
            Debug.LogWarning($"No toll sound found in {ArtFolder}. Save it there with the name toll (toll.wav, toll.ogg or toll.mp3) and run this again, or drag it into Toll Clip on the Conseraph object.");

        CustomPlayerController player = Object.FindFirstObjectByType<CustomPlayerController>();

        // if he is already in the scene he is updated where he stands, so his place, size, hand-placed parts and tuned numbers are all kept
        GameObject root = GameObject.Find("Conseraph");
        bool isNew = root == null;

        if (isNew)
        {
            root = new GameObject("Conseraph");
            root.transform.position = StartPosition(player);
        }

        // the halo is just a picture with no script.
        // it turns and moves with the rest of Conseraph and does nothing else.
        // the sorting order puts the halo at the very back, then the ring, then the face, with the mouth on top
        Transform halo = EnsurePart(root.transform, "Halo", haloSprite, -1, material);
        Transform ring = EnsurePart(root.transform, "Ring", ringSprite, 0, material);
        Transform face = EnsurePart(root.transform, "Face", faceSprite, 1, material);
        Transform mouth = EnsurePart(root.transform, "Mouth", mouthSprite, 2, material);

        // the trigger that notices the player touching him.
        // it has no Rigidbody on purpose: then it only reacts to the player's, and can't press pressure plates
        SphereCollider sphere = root.GetComponent<SphereCollider>();

        if (sphere == null)
        {
            sphere = root.AddComponent<SphereCollider>();
            sphere.radius = TouchRadius;
        }

        sphere.isTrigger = true;

        if (root.GetComponent<Conseraph>() == null)
            root.AddComponent<Conseraph>();

        ConseraphAudio audio = root.GetComponent<ConseraphAudio>();

        if (audio == null)
            audio = root.AddComponent<ConseraphAudio>();

        SerializedObject serializedAudio = new SerializedObject(audio);
        SerializedProperty tollProperty = serializedAudio.FindProperty("tollClip");

        // never replace a sound that is already there
        if (tollProperty.objectReferenceValue == null && tollClip != null)
        {
            tollProperty.objectReferenceValue = tollClip;
            serializedAudio.ApplyModifiedPropertiesWithoutUndo();
        }

        ConseraphVisuals visuals = root.GetComponent<ConseraphVisuals>();

        if (visuals == null)
        {
            visuals = root.AddComponent<ConseraphVisuals>();

            SerializedObject serialized = new SerializedObject(visuals);
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("ring").objectReferenceValue = ring;
            serialized.FindProperty("face").objectReferenceValue = face;
            serialized.FindProperty("mouth").objectReferenceValue = mouth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeObject = root;

        Debug.Log(isNew
            ? "Conseraph is built. Move him where you want him, then save the scene (Ctrl+S)."
            : "Conseraph is brought up to date, and everything you had set is kept. Save the scene (Ctrl+S).");
    }

    private static Vector3 StartPosition(CustomPlayerController player)
    {
        if (player == null)
            return Vector3.zero;

        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        return player.transform.position + forward * StartDistance + Vector3.up * StartHeight;
    }

    // finds the part under the root by name, or makes it.
    // the material is always set.
    // the picture is only filled in when the part has none, so a picture you set by hand is kept
    private static Transform EnsurePart(Transform parent, string partName, Sprite sprite, int sortingOrder, Material material)
    {
        Transform part = parent.Find(partName);

        if (part == null)
        {
            GameObject partObject = new GameObject(partName, typeof(SpriteRenderer));
            partObject.transform.SetParent(parent, false);
            part = partObject.transform;

            part.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
        }

        SpriteRenderer spriteRenderer = part.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = part.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
        }

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = sprite;

        spriteRenderer.sharedMaterial = material;

        return part;
    }

    // the material that draws him over every wall.
    // made the first time, then reused
    private static Material LoadMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material != null)
            return material;

        Shader shader = Shader.Find(ShaderName);

        if (shader == null || !shader.isSupported)
        {
            Debug.LogError($"The shader {ShaderName} is missing or has errors (see Assets/Shaders/ConseraphSprite.shader and the Console).");
            return null;
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);

        return material;
    }

    // the sound called toll, in any format, anywhere in the Conseraph folder
    private static AudioClip LoadToll()
    {
        foreach (string guid in AssetDatabase.FindAssets("toll t:AudioClip", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Path.GetFileNameWithoutExtension(path).ToLowerInvariant() == "toll")
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        return null;
    }

    // makes sure the picture is imported as a Sprite with its pivot in the middle, then loads it.
    // pass a pixelsPerUnit of 0 or less to leave the scale as it is
    private static Sprite LoadSprite(string path, float pixelsPerUnit)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            return null;

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

        if (importer == null)
            return null;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            changed = true;
        }

        if (pixelsPerUnit > 0f && !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
        {
            importer.spritePixelsPerUnit = pixelsPerUnit;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void WarnIfMissing(Sprite sprite, string path)
    {
        if (sprite == null)
            Debug.LogWarning($"Missing {path}, so that part has no picture. Save it there and run this again.");
    }
}
