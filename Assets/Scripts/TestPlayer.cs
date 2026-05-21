using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class TestPlayer : NetworkBehaviour
{
    public float moveSpeed = 5f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        CreateCamera();

        GetComponent<Renderer>().material.color =
            OwnerClientId == 0 ? Color.blue : Color.red;
    }

    void Update()
    {
        if (!IsOwner) return;

        Vector3 move = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            move.z += 1;

        if (Keyboard.current.sKey.isPressed)
            move.z -= 1;

        if (Keyboard.current.aKey.isPressed)
            move.x -= 1;

        if (Keyboard.current.dKey.isPressed)
            move.x += 1;

        move.Normalize();

        rb.linearVelocity = new Vector3(
            move.x * moveSpeed,
            rb.linearVelocity.y,
            move.z * moveSpeed
        );
    }

    void CreateCamera()
    {
        GameObject cam = new GameObject("PlayerCamera");

        Camera camera = cam.AddComponent<Camera>();

        cam.transform.position =
            transform.position + new Vector3(0, 10, -10);

        cam.transform.rotation =
            Quaternion.Euler(35, 0, 0);

        CameraFollow follow =
            cam.AddComponent<CameraFollow>();

        follow.target = transform;
    }
}