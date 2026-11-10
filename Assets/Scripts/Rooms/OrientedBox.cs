using UnityEngine;

// a box that can be turned and mirrored: a center plus three half-extent vectors (an axis direction times half the size along it)
// the three vectors stay at right angles under any rotation or mirror, so one transform handles a room turning, flipping, or pointing up
public struct OrientedBox
{
    // a box never gets thinner than this when shrunk, so it keeps its direction (a flat box still has a normal)
    private const float MinHalfSize = 0.01f;

    public Vector3 center;
    public Vector3 halfX;
    public Vector3 halfY;
    public Vector3 halfZ;

    public OrientedBox(Vector3 center, Vector3 halfX, Vector3 halfY, Vector3 halfZ)
    {
        this.center = center;
        this.halfX = halfX;
        this.halfY = halfY;
        this.halfZ = halfZ;
    }

    // a box from a local center and size, in whatever space the matrix leads to
    public static OrientedBox FromCenterSize(Vector3 center, Vector3 size, Matrix4x4 matrix)
    {
        Vector3 half = size * 0.5f;

        return new OrientedBox(
            matrix.MultiplyPoint3x4(center),
            matrix.MultiplyVector(new Vector3(half.x, 0f, 0f)),
            matrix.MultiplyVector(new Vector3(0f, half.y, 0f)),
            matrix.MultiplyVector(new Vector3(0f, 0f, half.z)));
    }

    public OrientedBox Transformed(Matrix4x4 matrix)
    {
        return new OrientedBox(
            matrix.MultiplyPoint3x4(center),
            matrix.MultiplyVector(halfX),
            matrix.MultiplyVector(halfY),
            matrix.MultiplyVector(halfZ));
    }

    // the same box with every half size reduced by amount, so boxes that only just touch or slightly overlap don't count
    public OrientedBox Shrunk(float amount)
    {
        return new OrientedBox(center, Shorten(halfX, amount), Shorten(halfY, amount), Shorten(halfZ, amount));
    }

    private static Vector3 Shorten(Vector3 half, float amount)
    {
        float length = half.magnitude;

        if (length <= amount + MinHalfSize)
            return length > 0.0001f ? half / length * MinHalfSize : Vector3.zero;

        return half * ((length - amount) / length);
    }

    // radius of a sphere that holds the whole box
    public float Radius => Mathf.Sqrt(halfX.sqrMagnitude + halfY.sqrMagnitude + halfZ.sqrMagnitude);

    // the separating axis test: two boxes are apart if there is any direction along which their shadows don't overlap
    // for two boxes that is the 3 axes of each, plus the 9 directions at right angles to a pair of edges
    public bool Overlaps(OrientedBox other)
    {
        Vector3 offset = other.center - center;

        // far apart boxes can't touch, no need for the full test
        float reach = Radius + other.Radius;

        if (offset.sqrMagnitude > reach * reach)
            return false;

        return !(
            IsSeparatedBy(halfX, other, offset) ||
            IsSeparatedBy(halfY, other, offset) ||
            IsSeparatedBy(halfZ, other, offset) ||
            IsSeparatedBy(other.halfX, other, offset) ||
            IsSeparatedBy(other.halfY, other, offset) ||
            IsSeparatedBy(other.halfZ, other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfX, other.halfX), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfX, other.halfY), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfX, other.halfZ), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfY, other.halfX), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfY, other.halfY), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfY, other.halfZ), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfZ, other.halfX), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfZ, other.halfY), other, offset) ||
            IsSeparatedBy(Vector3.Cross(halfZ, other.halfZ), other, offset));
    }

    private bool IsSeparatedBy(Vector3 axis, OrientedBox other, Vector3 offset)
    {
        float squared = axis.sqrMagnitude;

        // parallel edges give a zero direction, which can't separate anything
        if (squared < 0.0000001f)
            return false;

        axis /= Mathf.Sqrt(squared);

        float reachA = Mathf.Abs(Vector3.Dot(halfX, axis)) + Mathf.Abs(Vector3.Dot(halfY, axis)) + Mathf.Abs(Vector3.Dot(halfZ, axis));
        float reachB = Mathf.Abs(Vector3.Dot(other.halfX, axis)) + Mathf.Abs(Vector3.Dot(other.halfY, axis)) + Mathf.Abs(Vector3.Dot(other.halfZ, axis));

        return Mathf.Abs(Vector3.Dot(offset, axis)) > reachA + reachB;
    }

    // draws the 12 edges, for the Scene view
    public void DrawWire()
    {
        for (int i = 0; i < 4; i++)
        {
            // the four corners of the face on each side of the box, and the four edges joining the two faces
            float y = (i & 1) == 0 ? -1f : 1f;
            float z = (i & 2) == 0 ? -1f : 1f;

            Gizmos.DrawLine(Corner(-1f, y, z), Corner(1f, y, z));
            Gizmos.DrawLine(Corner(y, -1f, z), Corner(y, 1f, z));
            Gizmos.DrawLine(Corner(y, z, -1f), Corner(y, z, 1f));
        }
    }

    private Vector3 Corner(float x, float y, float z)
    {
        return center + halfX * x + halfY * y + halfZ * z;
    }
}
