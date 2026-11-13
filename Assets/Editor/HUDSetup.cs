using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// builds the HUD (room counter, speed, dash meter) in the open scene.
// safe to run again: it replaces the old "HUD" object
// needs Assets/UI/rounded_rect.png and Assets/UI/speed_label.png.
// save the scene afterwards (Ctrl+S)
public static class HUDSetup
{
    private const string RoundedRectPath = "Assets/UI/rounded_rect.png";
    private const string SpeedLabelPath = "Assets/UI/speed_label.png";
    private const string ClockFolder = "Assets/UI/clock";
    private const int ClockFrameCount = 8;
    private const string TickSoundPath = "Assets/UI/tick.mp3";
    private const string TimerFontFolder ="Assets/UI/fonts";
    private const string FontPath ="Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF.asset";

    [MenuItem("A Quick Sprint Through Hell/Set Up HUD")]
    public static void SetUp()
    {
        CustomPlayerController player = Object.FindFirstObjectByType<CustomPlayerController>();
        RoomChain roomChain = Object.FindFirstObjectByType<RoomChain>();

        if (player == null)
        {
            Debug.LogError("No CustomPlayerController found in the open scene. Open the scene with the Player first.");
            return;
        }

        Sprite rounded = LoadSprite(RoundedRectPath);

        if (rounded == null)
        {
            Debug.LogError($"Missing {RoundedRectPath}.");
            return;
        }

        Sprite speedLabel = LoadSprite(SpeedLabelPath);

        if (speedLabel == null)
            Debug.LogWarning($"Missing {SpeedLabelPath}, so the speed picture is left out. Save the picture there and run this again.");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (font == null)
            Debug.LogWarning("Anton SDF font not found, so the default font is used.");

        GameObject old = GameObject.Find("HUD");

        if (old != null)
            Object.DestroyImmediate(old);

        // the canvas, scaled so it looks the same on any screen size
        GameObject canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildSpeed(canvasObject.transform, player, speedLabel, font);
        BuildDashMeter(canvasObject.transform, player, rounded);

        if (roomChain != null)
        {
            BuildRoomCounter(canvasObject.transform, roomChain, font);
            BuildTimer(canvasObject.transform, roomChain, FindTimerFont() != null ? FindTimerFont() : font);
        }
        else
            Debug.LogWarning("No RoomChain found in the open scene, so the room counter and the countdown timer were not added.");

        EditorSceneManager.MarkSceneDirty(canvasObject.scene);
        Selection.activeObject = canvasObject;

        Debug.Log("HUD is set up in the top left: rooms, speed, then the dash meter. Bottom centre is kept free for the heartbeat. Save the scene (Ctrl+S).");
    }

    // ============================================================
    // PIECES
    // ============================================================

    private static void BuildSpeed(Transform canvas, CustomPlayerController player, Sprite label, TMP_FontAsset font)
    {
        GameObject root = new GameObject("Speed", typeof(RectTransform));
        root.transform.SetParent(canvas, false);

        RectTransform rootRect = (RectTransform)root.transform;
        Anchor(rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(27f, -105f), new Vector2(0f, 1f), new Vector2(507f, 100f));

        if (label != null)
        {
            GameObject labelObject = new GameObject("SpeedLabel", typeof(RectTransform), typeof(Image));
            labelObject.transform.SetParent(root.transform, false);

            Image image = labelObject.GetComponent<Image>();
            image.sprite = label;
            image.preserveAspect = true;
            image.raycastTarget = false;

            Anchor((RectTransform)labelObject.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 0.5f), new Vector2(270f, 100f));
        }

        TextMeshProUGUI number = MakeText("SpeedValue", root.transform, font, 80f, TextAlignmentOptions.Left);
        number.text = "0";
        number.color = Color.white;

        // the number grows from its left edge, so it moves away from the picture instead of over it
        Anchor(number.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(283f, 0f), new Vector2(0f, 0.5f), new Vector2(213f, 100f));

        SpeedHUD hud = root.AddComponent<SpeedHUD>();
        SetReference(hud, "player", player);
        SetReference(hud, "numberText", number);
    }

    private static void BuildDashMeter(Transform canvas, CustomPlayerController player, Sprite rounded)
    {
        GameObject root = new GameObject("DashMeter", typeof(RectTransform));
        root.transform.SetParent(canvas, false);

        Anchor((RectTransform)root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(27f, -210f), new Vector2(0f, 1f), new Vector2(400f, 40f));

        DashMeterHUD hud = root.AddComponent<DashMeterHUD>();
        SetReference(hud, "player", player);
        SetVector2(hud, "slotSize", new Vector2(60f, 27f));
        SetFloat(hud, "spacing", 9f);
        SetReference(hud, "slotSprite", rounded);
    }

    // the countdown at the top centre: the ticking clock on the left, the time on the right.
    // the whole group jitters (see CountdownTimerHUD)
    private static void BuildTimer(Transform canvas, RoomChain roomChain, TMP_FontAsset font)
    {
        GameObject root = new GameObject("Timer", typeof(RectTransform));
        root.transform.SetParent(canvas, false);

        Anchor((RectTransform)root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(0.5f, 1f), new Vector2(700f, 190f));

        // the clock pictures, clock0.png to clock7.png.
        // clock0 is the one it rests on
        Sprite[] frames = new Sprite[ClockFrameCount];

        for (int i = 0; i < ClockFrameCount; i++)
        {
            frames[i] = LoadSprite($"{ClockFolder}/clock{i}.png");

            if (frames[i] == null)
                Debug.LogWarning($"Missing {ClockFolder}/clock{i}.png, so that clock picture is left out. Save it there and run this again.");
        }

        GameObject clockObject = new GameObject("Clock", typeof(RectTransform), typeof(Image));
        clockObject.transform.SetParent(root.transform, false);

        Image clock = clockObject.GetComponent<Image>();
        clock.sprite = frames[0];
        clock.preserveAspect = true;
        clock.raycastTarget = false;
        clock.enabled = frames[0] != null;

        Anchor((RectTransform)clockObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-215f, 0f), new Vector2(0.5f, 0.5f), new Vector2(180f, 180f));

        TextMeshProUGUI time = MakeText("Time", root.transform, font, 150f, TextAlignmentOptions.Center);
        time.text = "05:10";
        time.color = Color.black;

        Anchor(time.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(95f, 0f), new Vector2(0.5f, 0.5f), new Vector2(420f, 180f));

        CountdownTimerHUD hud = root.AddComponent<CountdownTimerHUD>();
        SetReference(hud, "roomChain", roomChain);
        SetReference(hud, "clockImage", clock);
        SetReference(hud, "timeText", time);
        SetObjectArray(hud, "clockFrames", frames);
        SetReference(hud, "tickSound", AssetDatabase.LoadAssetAtPath<AudioClip>(TickSoundPath));
    }

    // the first TMP font asset in Assets/UI/fonts (the scary one), or null if there isn't one yet
    private static TMP_FontAsset FindTimerFont()
    {
        if (!AssetDatabase.IsValidFolder(TimerFontFolder))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { TimerFontFolder });

        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    private static void BuildRoomCounter(Transform canvas, RoomChain roomChain, TMP_FontAsset font)
    {
        TextMeshProUGUI text = MakeText("Rooms", canvas, font, 73f, TextAlignmentOptions.Left);
        text.text = "Rooms: 0";
        text.color = Color.black;

        Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(27f, -20f), new Vector2(0f, 1f), new Vector2(500f, 93f));

        RoomCounterHUD hud = text.gameObject.AddComponent<RoomCounterHUD>();
        SetReference(hud, "roomChain", roomChain);
        SetReference(hud, "text", text);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static TextMeshProUGUI MakeText(string objectName, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        return text;
    }

    private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 pivot, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    // makes sure the picture is imported as a Sprite (a plain Texture can't go in an Image), then loads it
    private static Sprite LoadSprite(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            return null;

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetObjectArray(Object target, string field, Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector2(Object target, string field, Vector2 value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).vector2Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);

        if (property == null)
        {
            Debug.LogWarning($"{target.GetType().Name} has no field '{field}'.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
