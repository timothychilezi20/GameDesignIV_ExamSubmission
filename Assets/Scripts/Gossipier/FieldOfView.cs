using UnityEngine;

public class FieldOfView : MonoBehaviour
{
    public float fov = 90f;
    public float viewDistance = 50f;
    public int rayCount = 50;
    public LayerMask obstacleMask; // Assign in Inspector — set to whatever layer your walls/objects are on

    private Mesh _mesh;

    private void Start()
    {
        _mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = _mesh;
    }

    // Changed: Start → Update
    // FOV needs to redraw every frame as the NPC rotates/moves.
    // In Start it only ever draws once at launch.
    private void Update()
    {
        float angle = fov / 2f; // Start at left edge of cone
        float angleIncrease = fov / rayCount;

        Vector3 origin = Vector3.zero;

        Vector3[] vertices = new Vector3[rayCount + 1 + 1];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = origin;

        int vertexIndex = 1;
        int triangleIndex = 0;

        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 direction = GetVectorFromAngle(angle);

            // Changed: Physics2D.Raycast → Physics.Raycast
            // Physics.Raycast detects 3D colliders (BoxCollider,
            // CapsuleCollider etc) which is what your scene objects use.
            // The origin is transformed to world space so the ray fires
            // from the NPC's actual position, not just Vector3.zero.
            Vector3 vertex;
            if (Physics.Raycast(transform.position, transform.TransformDirection(direction), out RaycastHit hit, viewDistance, obstacleMask))
            {
                // Hit something — stop the ray at the hit point.
                // Convert back to local space for the mesh vertex.
                vertex = transform.InverseTransformPoint(hit.point);
            }
            else
            {
                // No hit — ray goes full distance
                vertex = direction * viewDistance;
            }

            vertices[vertexIndex] = vertex;

            if (i > 0)
            {
                triangles[triangleIndex + 0] = 0;
                triangles[triangleIndex + 1] = vertexIndex - 1;
                triangles[triangleIndex + 2] = vertexIndex;
                triangleIndex += 3;
            }

            vertexIndex++;
            angle -= angleIncrease;
        }

        _mesh.vertices = vertices;
        _mesh.uv = uv;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
    }

    // Changed: XY plane → XZ plane
    // FOV cone sits flat on the ground
    // XY was correct for 2D but in 3D the horizontal plane is XZ.
    // Y is now 0 (flat) and Z replaces what Y was doing.
    public static Vector3 GetVectorFromAngle(float angle)
    {
        float angleRad = angle * (Mathf.PI / 180f);

        // Changed: Cos and Sin swapped.
        // Sin(0) = 0 on X and Cos(0) = 1 on Z, meaning angle 0
        // now points straight forward along the Z axis — matching
        // Unity's world forward and the gossipier's transform.forward.
        return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad));
    }
}
