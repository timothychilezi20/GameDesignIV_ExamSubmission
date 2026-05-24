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

    // Input
    private Vector2 moveInput;
    private Vector2 lookInput;

    // Movement
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    private float verticalVelocity;

    // Camera reference
    private Transform camTransform;

    [Header("Smoothing")]
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;

    private Vector3 currentVelocity;
    private Vector3 smoothDirection;

    public override void OnNetworkSpawn()
    {
        // ---------------- SERVER SPAWN ----------------
        if (IsServer)
        {
            GameObject spawnPoint = null;

            if (OwnerClientId == NetworkManager.ServerClientId)
                spawnPoint = GameObject.Find("HostSpawn");
            else
                spawnPoint = GameObject.Find("ClientSpawn");

            if (spawnPoint != null)
            {
                transform.position = spawnPoint.transform.position;
                transform.rotation = spawnPoint.transform.rotation;
            }
        }

        // ---------------- NON-OWNER ----------------
        if (!IsOwner)
        {
            if (controller != null)
                controller.enabled = false;

            return;
        }

        // ---------------- LOCAL PLAYER ----------------
        if (controller != null)
            controller.enabled = true;

        controls = new PlayerControls();
        controls.Enable();
        controls.PlayerMovement.SetCallbacks(this);

        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        verticalVelocity = 0f;

        // IMPORTANT: delay camera assignment (FIXES YOUR ISSUE)
        StartCoroutine(AssignCamera());

        Debug.Log($"[OnNetworkSpawn] Player ready: {OwnerClientId}");
    }

    private IEnumerator AssignCamera()
    {
        // Wait for camera to exist in scene
        yield return null;

        mainCam = Camera.main;

        if (mainCam == null)
        {
            Debug.LogError("Main Camera not found!");
            yield break;
        }

        camTransform = mainCam.transform;

        ThirdPersonCamera camFollow = mainCam.GetComponent<ThirdPersonCamera>();

        if (camFollow == null)
        {
            Debug.LogError("ThirdPersonCamera missing on Main Camera!");
            yield break;
        }

        camFollow.SetTarget(transform);

        Debug.Log("Camera successfully attached to player: " + name);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        controls.PlayerMovement.RemoveCallbacks(this);
        controls.Disable();
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
    }

    // ---------------- INPUT ----------------

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        lookInput = context.ReadValue<Vector2>();
    }

    // ---------------- MOVEMENT (OPTION A - CORRECT) ----------------

    private void HandleMovement()
    {
        if (camTransform == null) return;

        Vector3 forward = camTransform.forward;
        Vector3 right = camTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // RAW input direction (DO NOT SMOOTH THIS)
        Vector3 inputDirection =
            forward * moveInput.y +
            right * moveInput.x;

        if (inputDirection.magnitude > 1f)
            inputDirection.Normalize();

        // ---------------- ROTATION (smooth only) ----------------
        if (inputDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                12f * Time.deltaTime
            );
        }

        // ---------------- GRAVITY ----------------
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        // ---------------- SMOOTH ACCELERATION (velocity ONLY) ----------------
        Vector3 targetVelocity = inputDirection * moveSpeed;

        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);

        currentHorizontalVelocity = Vector3.Lerp(
            currentHorizontalVelocity,
            targetVelocity,
            10f * Time.deltaTime
        );

        currentVelocity = new Vector3(
            currentHorizontalVelocity.x,
            verticalVelocity,
            currentHorizontalVelocity.z
        );

        controller.Move(currentVelocity * Time.deltaTime);
    }
}