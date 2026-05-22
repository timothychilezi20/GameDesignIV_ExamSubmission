using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour, PlayerControls.IPlayerMovementActions
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;

    private PlayerControls controls;

    private Vector2 moveInput;
    private Vector2 lookInput;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 10f;
    public float drag = 8f;

    private Vector3 velocity;

    [Header("Camera Look")]
    public float lookSensitivity = 2f;
    public float lookLimit = 80f;

    private float yaw;
    private float pitch;

    [Header("Camera")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -4f);
    [SerializeField] private float cameraFollowSpeed = 8f;

    public override void OnNetworkSpawn()
    {
        // Only local player sees/controls camera logic
        if (IsOwner)
        {
            controls = new PlayerControls();
            controls.Enable();
            controls.PlayerMovement.SetCallbacks(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        controls.PlayerMovement.RemoveCallbacks(this);
        controls.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        HandleLook();
        HandleCamera(); 
    }

    // ---------------- INPUT CALLBACKS ----------------

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    // ---------------- MOVEMENT ----------------

    void HandleMovement()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            right * moveInput.x +
            forward * moveInput.y;

        velocity += direction * acceleration * Time.deltaTime;

        // Apply drag
        velocity = Vector3.Lerp(velocity, Vector3.zero, drag * Time.deltaTime);

        velocity = Vector3.ClampMagnitude(velocity, moveSpeed);

        characterController.Move(velocity * Time.deltaTime);
    }

    // ---------------- THIRD-PERSON LOOK ----------------

    void HandleLook()
    {
        if (!IsOwner) return;

        yaw += lookInput.x * lookSensitivity;
        pitch -= lookInput.y * lookSensitivity;

        pitch = Mathf.Clamp(pitch, -lookLimit, lookLimit);

        // ONLY rotate player OR rig (NOT cameraTarget)
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void HandleCamera()
    {
        if (!IsOwner || playerCamera == null) return;

        Vector3 targetPosition = transform.position + transform.rotation * cameraOffset;

        playerCamera.position = Vector3.Lerp(
            playerCamera.position,
            targetPosition,
            cameraFollowSpeed * Time.deltaTime
        );

        playerCamera.LookAt(transform.position + Vector3.up * 1.5f);
    }
}