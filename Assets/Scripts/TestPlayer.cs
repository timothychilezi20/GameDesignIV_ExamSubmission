using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class TestPlayer : NetworkBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector3 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = true;
        rb.isKinematic = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        CreateCamera();

        Renderer rend = GetComponent<Renderer>();

        if (rend != null)
        {
            rend.material.color =
                OwnerClientId == 0 ? Color.blue : Color.red;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        moveInput = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            moveInput.z += 1;

        if (Keyboard.current.sKey.isPressed)
            moveInput.z -= 1;

        if (Keyboard.current.aKey.isPressed)
            moveInput.x -= 1;

        if (Keyboard.current.dKey.isPressed)
            moveInput.x += 1;

        moveInput.Normalize();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        rb.linearVelocity = new Vector3(
            moveInput.x * moveSpeed,
            rb.linearVelocity.y,
            moveInput.z * moveSpeed
        );
    }

    void CreateCamera()
    {
        Camera existingCam = Camera.main;

        if (existingCam != null)
        {
            existingCam.transform.SetParent(transform);

            existingCam.transform.localPosition =
                new Vector3(0, 10, -10);

            existingCam.transform.localRotation =
                Quaternion.Euler(35, 0, 0);

            CameraFollow follow =
                existingCam.GetComponent<CameraFollow>();

            if (follow == null)
                follow = existingCam.gameObject.AddComponent<CameraFollow>();

            follow.target = transform;
        }
    }
}