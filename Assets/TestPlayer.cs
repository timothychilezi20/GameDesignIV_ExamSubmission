using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class TestPlayer : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb;
    private Camera playerCam;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Disable control for non-owners
        if (!IsOwner)
            return;

        // Create simple follow camera
        CreateCamera();

        // Differentiate players visually
        Renderer renderer = GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.material.color =
                OwnerClientId == 0 ? Color.blue : Color.red;
        }

        Debug.Log($"Player spawned. Owner: {OwnerClientId}");
    }

    void Update()
    {
        if (!IsOwner) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0f, v);

        rb.linearVelocity = new Vector3(
            move.x * moveSpeed,
            rb.linearVelocity.y,
            move.z * moveSpeed
        );
    }

    void CreateCamera()
    {
        GameObject camObj = new GameObject("PlayerCamera");

        playerCam = camObj.AddComponent<Camera>();

        camObj.transform.position =
            transform.position + new Vector3(0, 10, -10);

        camObj.transform.rotation =
            Quaternion.Euler(35f, 0f, 0f);

        CameraFollow follow =
            camObj.AddComponent<CameraFollow>();

        follow.target = transform;
    }
}