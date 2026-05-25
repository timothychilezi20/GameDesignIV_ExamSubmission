using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : NetworkBehaviour, GameInput.IPlayerMovementActions
{
    [Header("References")]
    [SerializeField] private CharacterController controller;

    private Camera mainCam;
    private GameInput controls;

    private Vector2 moveInput;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Smoothing")]
    [SerializeField] private float acceleration = 10f;

    private float verticalVelocity;
    private Vector3 currentVelocity;

    private Transform camTransform;

    // ---------------- NETWORK SPAWN ----------------

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameObject spawnPoint = OwnerClientId == NetworkManager.ServerClientId
                ? GameObject.Find("HostSpawn")
                : GameObject.Find("ClientSpawn");

            if (spawnPoint != null)
            {
                transform.position = spawnPoint.transform.position;
                transform.rotation = spawnPoint.transform.rotation;
            }
        }

        if (!IsOwner)
        {
            if (controller != null)
                controller.enabled = false;

            return;
        }

        controller.enabled = true;

        controls = new GameInput();
        controls.Enable();
        controls.PlayerMovement.SetCallbacks(this);

        moveInput = Vector2.zero;
        verticalVelocity = 0f;

        StartCoroutine(AssignCamera());
    }

    private IEnumerator AssignCamera()
    {
        yield return null;

        mainCam = Camera.main;

        if (mainCam == null) yield break;

        camTransform = mainCam.transform;

        ThirdPersonCamera camFollow = mainCam.GetComponent<ThirdPersonCamera>();

        if (camFollow != null)
        {
            camFollow.SetTarget(transform);
        }
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
        if (!IsOwner) return;

        HandleMovement();
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

        // Rotate toward movement
        if (inputDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(inputDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                12f * Time.deltaTime
            );
        }

        // Gravity
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        // Smooth movement
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
    }
}