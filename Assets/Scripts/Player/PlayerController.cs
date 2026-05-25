using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : NetworkBehaviour, PlayerControls.IPlayerMovementActions
{
    [Header("References")]
    [SerializeField] private CharacterController controller;

    private Camera mainCam;
    private PlayerControls controls;
    private Vector2 moveInput;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Smoothing")]
    [SerializeField] private float acceleration = 10f;

    private float verticalVelocity;
    private Vector3 currentVelocity;
    private Transform camTransform;

    // ─── Added: NetworkVariables for remote player interpolation ──
    // The owner updates these every frame via ServerRpc.
    // Remote clients read them in Update to smoothly interpolate
    // the non-owned player object instead of snapping/teleporting.
    private NetworkVariable<Vector3> _networkPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<Quaternion> _networkRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ─── Flag to prevent lerping before first valid position arrives ───
    private bool _hasReceivedFirstPosition = false;
    // ─────────────────────────────────────────────────────────────

    // ─── Area Tracking ────────────────────────────────────────────
    public string CurrentAreaName { get; private set; } = "Unknown Area";
    public SchoolArea.AreaType CurrentAreaType { get; private set; }
    public bool IsInArea { get; private set; } = false;
    // ─────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (controller != null)
                controller.enabled = false;

            _networkPosition.OnValueChanged += OnNetworkPositionFirstReceived;
            return;
        }

        controller.enabled = true;

        controls = new PlayerControls();
        controls.Enable();
        controls.PlayerMovement.SetCallbacks(this);

        moveInput = Vector2.zero;
        verticalVelocity = 0f;

        StartCoroutine(AssignCamera());
    }

    private void OnNetworkPositionFirstReceived(Vector3 previous, Vector3 current)
    {
       if (_hasReceivedFirstPosition) return;

        //Snap directly to spawn position, don't lerp from Vector3.zero
        transform.position = current;
        _hasReceivedFirstPosition = true;

        _networkPosition.OnValueChanged -= OnNetworkPositionFirstReceived;
    }

    private IEnumerator AssignCamera()
    {
        yield return null;

        mainCam = Camera.main;
        if (mainCam == null) yield break;

        camTransform = mainCam.transform;

        ThirdPersonCamera camFollow = mainCam.GetComponent<ThirdPersonCamera>();
        if (camFollow != null)
            camFollow.SetTarget(transform);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        controls.PlayerMovement.RemoveCallbacks(this);
        controls.Disable();
    }

    // ---------------- INPUT ----------------

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context) { }
    public void OnPause(InputAction.CallbackContext context) { }

    // ---------------- UPDATE ----------------

    private void Update()
    {
        if (IsOwner)
        {
            HandleMovement();
        }
        else
        {
            // ─── Added: Remote player interpolation ───────────────
            // Non-owned players read the NetworkVariables written by
            // the owner's ServerRpc and smoothly lerp toward them.
            // Without this remote players snap to new positions
            // every network tick instead of moving fluidly.

            if (!_hasReceivedFirstPosition) return; 

            transform.position = Vector3.Lerp(
                transform.position,
                _networkPosition.Value,
                Time.deltaTime * 15f
            );
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                _networkRotation.Value,
                Time.deltaTime * 15f
            );
            // ─────────────────────────────────────────────────────
        }
    }

    // ---------------- MOVEMENT ----------------

    private void HandleMovement()
    {
        if (camTransform == null) return;

        Vector3 forward = camTransform.forward;
        Vector3 right = camTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 inputDirection =
            forward * moveInput.y +
            right * moveInput.x;

        if (inputDirection.magnitude > 1f)
            inputDirection.Normalize();

        if (inputDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                12f * Time.deltaTime
            );
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 targetVelocity = inputDirection * moveSpeed;
        Vector3 horizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        horizontal = Vector3.Lerp(
            horizontal,
            targetVelocity,
            acceleration * Time.deltaTime
        );

        currentVelocity = new Vector3(
            horizontal.x,
            verticalVelocity,
            horizontal.z
        );

        controller.Move(currentVelocity * Time.deltaTime);

        // ─── Added: Send position and rotation to server ──────────
        // After moving locally, the owner tells the server its new
        // transform so the server can update the NetworkVariables
        // which replicate out to all other connected clients.
        UpdateTransformServerRpc(transform.position, transform.rotation);
        // ─────────────────────────────────────────────────────────
    }

    // ─── Added: ServerRpc ─────────────────────────────────────────
    // Runs on the server when called by the owning client.
    // Updates NetworkVariables which automatically broadcast
    // to all other clients on the next network tick.
    [ServerRpc]
    private void UpdateTransformServerRpc(Vector3 position, Quaternion rotation)
    {
        _networkPosition.Value = position;
        _networkRotation.Value = rotation;
    }
    // ─────────────────────────────────────────────────────────────

    // ─── Area Tracking Triggers ───────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        SchoolArea area = other.GetComponent<SchoolArea>();
        if (area == null) return;

        CurrentAreaName = area.areaName;
        CurrentAreaType = area.areaType;
        IsInArea = true;
    }

    private void OnTriggerExit(Collider other)
    {
        SchoolArea area = other.GetComponent<SchoolArea>();
        if (area == null) return;

        IsInArea = false;
        CurrentAreaName = "Unknown Area";
    }
    // ─────────────────────────────────────────────────────────────
}