using UnityEditor;
using UnityEngine;

// Builds two simple placeholder rooms out of plain cubes, so you can try the room stitching before you have
// real rooms. Delete this file and Assets/Prefabs/Rooms once you do.
//
//   TestRoom_A: a straight platform, the Exit is straight ahead.
//   TestRoom_B: a square platform, the Exit is on the right side, so the path turns 90 degrees.
public static class TestRoomBuilder
{
    private const string Folder = "Assets/Prefabs/Rooms";
    private const string GreyMaterialPath = "Assets/Textures/Prototype/general/Materials/prototype_grid_grey.mat";
    private const string OrangeMaterialPath = "Assets/Textures/Prototype/general/Materials/prototype_grid_orange.mat";

    // The gate opening. These match the gizmo defaults in RoomGate.
    private const float OpeningWidth = 8f;
    private const float OpeningHeight = 18f;
    private const float FrameThickness = 1f;

    [MenuItem("A Quick Sprint Through Hell/Create Test Rooms")]
    public static void CreateTestRooms()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Rooms");

        Material grey = AssetDatabase.LoadAssetAtPath<Material>(GreyMaterialPath);
        Material orange = AssetDatabase.LoadAssetAtPath<Material>(OrangeMaterialPath);

        float gateY = OpeningHeight * 0.5f;

        // Floor top is at y = 0, so a gate centered at y = 9 has the bottom of its opening level with the floor.
        // That is just what these test rooms do, a gate can be placed at any height.
        Room roomA = BuildRoom(
            "TestRoom_A", grey,
            floorSize: new Vector2(20f, 60f),
            exitPosition: new Vector3(0f, gateY, 60f),
            exitYaw: 0f);

        Room roomB = BuildRoom(
            "TestRoom_B", orange,
            floorSize: new Vector2(40f, 40f),
            exitPosition: new Vector3(20f, gateY, 20f),
            exitYaw: 90f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SetUpChainInScene(new[] { roomA, roomB });

        Debug.Log("Test rooms created in " + Folder + ". Put the Player on the floor of the first room, for example at (0, 1, 5), and press Play.");
    }

    private static Room BuildRoom(string roomName, Material material, Vector2 floorSize, Vector3 exitPosition, float exitYaw)
    {
        GameObject root = new GameObject(roomName);
        root.AddComponent<Room>();

        // The floor starts at the Entry gate (z = 0) and runs forward.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localScale = new Vector3(floorSize.x, 1f, floorSize.y);
        floor.transform.localPosition = new Vector3(0f, -0.5f, floorSize.y * 0.5f);
        ApplyMaterial(floor, material);

        CreateGate(root.transform, "Entry", GateType.Entry, new Vector3(0f, OpeningHeight * 0.5f, 0f), 0f, material);
        CreateGate(root.transform, "Exit", GateType.Exit, exitPosition, exitYaw, material);

        string path = Folder + "/" + roomName + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<Room>();
    }

    private static void CreateGate(Transform parent, string gateName, GateType type, Vector3 localPosition, float yaw, Material material)
    {
        GameObject gate = new GameObject(gateName);
        gate.transform.SetParent(parent, false);
        gate.transform.localPosition = localPosition;
        gate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        RoomGate roomGate = gate.AddComponent<RoomGate>();
        roomGate.Type = type;
        roomGate.OpeningSize = new Vector2(OpeningWidth, OpeningHeight);

        // A frame made of two posts and a top beam. There is no bottom bar, the opening goes all the way down.
        float halfWidth = OpeningWidth * 0.5f;
        float halfHeight = OpeningHeight * 0.5f;
        float postHeight = OpeningHeight + FrameThickness;

        AddFramePiece(gate.transform, "LeftPost",
            new Vector3(-(halfWidth + FrameThickness * 0.5f), FrameThickness * 0.5f, 0f),
            new Vector3(FrameThickness, postHeight, FrameThickness), material);

        AddFramePiece(gate.transform, "RightPost",
            new Vector3(halfWidth + FrameThickness * 0.5f, FrameThickness * 0.5f, 0f),
            new Vector3(FrameThickness, postHeight, FrameThickness), material);

        AddFramePiece(gate.transform, "TopBeam",
            new Vector3(0f, halfHeight + FrameThickness * 0.5f, 0f),
            new Vector3(OpeningWidth, FrameThickness, FrameThickness), material);
    }

    private static void AddFramePiece(Transform parent, string pieceName, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = pieceName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = scale;
        ApplyMaterial(piece, material);
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        if (material != null)
            target.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // Finds (or makes) a RoomChain in the open scene and gives it the test rooms.
    private static void SetUpChainInScene(Room[] rooms)
    {
        RoomChain chain = Object.FindFirstObjectByType<RoomChain>();

        if (chain == null)
        {
            GameObject chainObject = new GameObject("RoomChain");
            Undo.RegisterCreatedObjectUndo(chainObject, "Create RoomChain");
            chain = chainObject.AddComponent<RoomChain>();
        }

        SerializedObject serialized = new SerializedObject(chain);
        SerializedProperty list = serialized.FindProperty("roomPrefabs");
        list.arraySize = rooms.Length;

        for (int i = 0; i < rooms.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];

        serialized.ApplyModifiedProperties();
        Selection.activeObject = chain.gameObject;
    }
}
