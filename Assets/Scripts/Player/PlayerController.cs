using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Camera _playerCamera;

    public float runAcceleration = 0.25f;
    public float runSpeed = 4f;
    public float drag = 0.1f;
    public float lookSenseH = 0.1f;
    public float lookSenseV = 0.1f;
    public float lookLimitV = 89f;

    private PlayerMovement _playerMovement;
    private Vector2 _cameraRotation = Vector2.zero;
    private Vector2 _playerTargetRotation = Vector2.zero;

    // Added: local velocity cache for smooth CharacterController movement.
    private Vector3 _currentVelocity;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
    }

    // Added: OnNetworkSpawn to disable the camera for remote players.
    // Without this every spawned player would have an active camera,
    // causing the local view to flicker or show the wrong perspective.
    public override void OnNetworkSpawn()
    {
        _playerCamera.gameObject.SetActive(IsOwner);
    }

    private void Update()
    {
        // Only the owning client controls this player.
        // NetworkTransform automatically syncs the movement
        // and rotation to all remote clients.
        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector3 cameraForwardXZ = new Vector3(
            _playerCamera.transform.forward.x,
            0f,
            _playerCamera.transform.forward.z
        ).normalized;

        Vector3 cameraRightXZ = new Vector3(
            _playerCamera.transform.right.x,
            0f,
            _playerCamera.transform.right.z
        ).normalized;

        Vector3 movementDirection =
            cameraRightXZ * _playerMovement.MovementInput.x +
            cameraForwardXZ * _playerMovement.MovementInput.y;

        Vector3 movementDelta =
            movementDirection * runAcceleration * Time.deltaTime;

        // Build velocity over time
        _currentVelocity += movementDelta;

        // Apply drag
        Vector3 currentDrag =
            _currentVelocity.normalized * drag * Time.deltaTime;

        _currentVelocity =
            (_currentVelocity.magnitude > drag * Time.deltaTime)
            ? _currentVelocity - currentDrag
            : Vector3.zero;

        // Clamp speed
        _currentVelocity =
            Vector3.ClampMagnitude(_currentVelocity, runSpeed);

        // Move using CharacterController
        _characterController.Move(
            _currentVelocity * Time.deltaTime
        );
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        _cameraRotation.x +=
            lookSenseH * _playerMovement.LookInput.x;

        _cameraRotation.y = Mathf.Clamp(
            _cameraRotation.y -
            lookSenseV * _playerMovement.LookInput.y,
            -lookLimitV,
            lookLimitV
        );

        _playerTargetRotation.x +=
            lookSenseH * _playerMovement.LookInput.x;

        // Rotate player body
        transform.rotation = Quaternion.Euler(
            0f,
            _playerTargetRotation.x,
            0f
        );

        // Rotate camera independently
        _playerCamera.transform.rotation = Quaternion.Euler(
            _cameraRotation.y,
            _cameraRotation.x,
            0f
        );
    }
}