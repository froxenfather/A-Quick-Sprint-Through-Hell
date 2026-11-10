using UnityEngine;
using UnityEngine.ProBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

// a flat slab (wall, floor or ceiling) with a rectangular hole cut out where a gate is
// move the gate and the slab rebuilds itself around it, so the opening follows the gate

// the slab is built out of up to four ProBuilder boxes around the opening: left, right, below and above
// pieces that would have no size (the gate touches an edge) are skipped

// the slab is centered on this object. width runs along its X, height along its Y, and thickness along its Z
// point the gate the same way (its blue arrow along the slab's Z, or the opposite way). the gate can be rolled 90 degrees, the opening is measured in the slab's space. keep the scale of this object and the gate at (1, 1, 1)
// the pieces are rebuilt in the editor only. they are saved with the scene or prefab, so rooms spawned at runtime already have them
[ExecuteAlways]
public class WallWithOpening : MonoBehaviour
{
    private const string ContainerName = "Wall Pieces";
    private const float MinPieceSize = 0.001f;

    // how long (in seconds) nothing has to change before the pieces are rebuilt
    private const double SettleSeconds = 0.35;

    // how closely the gate has to face along the slab's thickness (1 = exactly)
    private const float ParallelTolerance = 0.99f;

    [SerializeField] private Vector2 size = new Vector2(40f, 20f);
    [SerializeField] private float thickness = 1f;

    // the gate that cuts the hole. leave empty for a solid slab
    [SerializeField] private RoomGate gate;

    [SerializeField] private Material material;

    // draws the slab's size as a cyan box in the Scene view while it is selected. off unless you want it
    [SerializeField] private bool drawOutline = false;

    // everything the pieces depend on, so we can tell when they are out of date
    private struct BuildState
    {
        public bool hasGate;
        public Vector3 gatePosition;
        public Quaternion gateRotation;
        public Vector2 openingSize;
        public Vector2 size;
        public float thickness;
        public Material material;
    }

    // what the pieces were last built from
    private BuildState builtState;

    // what we saw on the previous editor tick. while this keeps changing, something is still being dragged
    private BuildState seenState;

    private double lastChangeTime;

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        if (transform.Find(ContainerName) == null)
            Rebuild();
        else
            RememberState();

        seenState = builtState;

#if UNITY_EDITOR
        EditorApplication.update += CheckForChanges;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= CheckForChanges;
#endif
    }

#if UNITY_EDITOR
    // runs every editor tick. the pieces are only rebuilt once nothing has changed for a moment, so dragging a gate
    // or a size slider does not rebuild (and re-save the prefab) on every single step
    private void CheckForChanges()
    {
        // rooms spawned at runtime already have their pieces, nothing to do
        if (Application.isPlaying)
            return;

        BuildState current = ReadState();

        if (!Same(current, seenState))
        {
            seenState = current;
            lastChangeTime = EditorApplication.timeSinceStartup;
            return;
        }

        if (!Same(current, builtState) && EditorApplication.timeSinceStartup - lastChangeTime >= SettleSeconds)
            Rebuild();
    }
#endif

    [ContextMenu("Rebuild")]
    private void Rebuild()
    {
        Transform existing = transform.Find(ContainerName);

        if (existing != null)
            DestroyPieces(existing.gameObject);

        GameObject container = new GameObject(ContainerName);
        container.transform.SetParent(transform, false);

        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        if (!TryGetOpening(halfWidth, halfHeight, out Vector2 openingMin, out Vector2 openingMax))
        {
            // no gate, or it is nowhere near the slab, so the slab stays solid
            AddPiece(container.transform, "Solid", -halfWidth, halfWidth, -halfHeight, halfHeight);
            RememberState();
            return;
        }

        AddPiece(container.transform, "Left", -halfWidth, openingMin.x, -halfHeight, halfHeight);
        AddPiece(container.transform, "Right", openingMax.x, halfWidth, -halfHeight, halfHeight);
        AddPiece(container.transform, "Below", openingMin.x, openingMax.x, -halfHeight, openingMin.y);
        AddPiece(container.transform, "Above", openingMin.x, openingMax.x, openingMax.y, halfHeight);

        RememberState();
    }

    // works out where the gate's opening lands on the slab, in the slab's own X and Y
    private bool TryGetOpening(float halfWidth, float halfHeight, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (gate == null)
            return false;

        if (Mathf.Abs(Vector3.Dot(gate.transform.forward, transform.forward)) < ParallelTolerance)
        {
            Debug.LogWarning($"Gate '{gate.name}' is not facing through the slab '{name}', so no hole was cut. Turn the gate so its blue arrow points through the slab.", this);
            return false;
        }

        // take the four corners of the opening into the slab's space. using the corners means a gate rolled 90 degrees still gets the right width and height
        Vector2 halfOpening = gate.OpeningSize * 0.5f;

        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 worldCorner = gate.transform.TransformPoint(new Vector3(x * halfOpening.x, y * halfOpening.y, 0f));
                Vector3 localCorner = transform.InverseTransformPoint(worldCorner);

                min.x = Mathf.Min(min.x, localCorner.x);
                min.y = Mathf.Min(min.y, localCorner.y);
                max.x = Mathf.Max(max.x, localCorner.x);
                max.y = Mathf.Max(max.y, localCorner.y);
            }
        }

        // a gate sticking out past the slab only cuts the part that is inside it
        min.x = Mathf.Max(min.x, -halfWidth);
        min.y = Mathf.Max(min.y, -halfHeight);
        max.x = Mathf.Min(max.x, halfWidth);
        max.y = Mathf.Min(max.y, halfHeight);

        // nothing of the opening is on the slab
        return max.x - min.x > MinPieceSize && max.y - min.y > MinPieceSize;
    }

    private void AddPiece(Transform parent, string pieceName, float minX, float maxX, float minY, float maxY)
    {
        if (maxX - minX < MinPieceSize || maxY - minY < MinPieceSize)
            return;

        // the box is built at its real size, so the piece keeps a scale of (1, 1, 1) and ProBuilder's UVs tile evenly
        ProBuilderMesh piece = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(maxX - minX, maxY - minY, thickness));
        piece.name = pieceName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);

        piece.ToMesh();
        piece.Refresh();

        piece.GetComponent<MeshRenderer>().sharedMaterial = material != null ? material : BuiltinMaterials.defaultMaterial;

        // built outside the ProBuilder editor menus, so nothing adds the collider for us
        piece.gameObject.AddComponent<MeshCollider>();
    }

    // deletes the pieces and their meshes. destroying a GameObject on its own leaves its mesh behind,
    // so without this every rebuild would leak a few meshes into memory
    private static void DestroyPieces(GameObject container)
    {
#if UNITY_EDITOR
        foreach (MeshFilter filter in container.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;

            // meshes saved inside an asset are not ours to destroy
            if (mesh != null && !EditorUtility.IsPersistent(mesh))
                DestroyImmediate(mesh);
        }
#endif

        DestroyImmediate(container);
    }

    private void RememberState()
    {
        builtState = ReadState();
    }

    // the gate is measured relative to the slab, so moving the whole room around does not count as a change
    private BuildState ReadState()
    {
        BuildState state = new BuildState
        {
            hasGate = gate != null,
            size = size,
            thickness = thickness,
            material = material
        };

        if (gate != null)
        {
            state.gatePosition = transform.InverseTransformPoint(gate.transform.position);
            state.gateRotation = Quaternion.Inverse(transform.rotation) * gate.transform.rotation;
            state.openingSize = gate.OpeningSize;
        }

        return state;
    }

    private static bool Same(BuildState a, BuildState b)
    {
        return a.hasGate == b.hasGate
            && a.size == b.size
            && a.thickness == b.thickness
            && a.material == b.material
            && a.gatePosition == b.gatePosition
            && a.gateRotation == b.gateRotation
            && a.openingSize == b.openingSize;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOutline)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, thickness));

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
