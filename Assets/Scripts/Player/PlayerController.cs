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

    // Added: NetworkVariable to sync position to all clients.
    // ServerWrite means only the server can change it;
    // all clients can read it to render the remote player correctly.
    private NetworkVariable<Vector3> _networkPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Added: NetworkVariable to sync rotation.
    // Remote clients read this to rotate the non-owned player object.
    private NetworkVariable<Quaternion> _networkRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );


    // ─── Area Tracking ────────────────────────────────────────────
    // Added: These three properties are read by the Gossipier's
    // LogPlayerSpotted method when the FOV detects this player.
    // Stored directly on the player so any gossipier can query it
    // without needing to know which area they're patrolling.
    public string CurrentAreaName { get; private set; } = "Unknown Area";
    public SchoolArea.AreaType CurrentAreaType { get; private set; }
    public bool IsInArea { get; private set; } = false;

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
        if (IsOwner)
        {
            HandleMovement();
        }
        else
        {
            // Remote players: smoothly interpolate to their
            // server-synced position and rotation instead of
            // snapping, which would look jittery.
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
        }
    }

    private void HandleMovement()
    {
        Vector3 cameraForwardXZ = new Vector3(_playerCamera.transform.forward.x, 0f, _playerCamera.transform.forward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(_playerCamera.transform.right.x, 0f, _playerCamera.transform.right.z).normalized;
        Vector3 movementDirection = cameraRightXZ * _playerMovement.MovementInput.x + cameraForwardXZ * _playerMovement.MovementInput.y;

        Vector3 movementDelta = movementDirection * runAcceleration * Time.deltaTime;
        Vector3 newVelocity = _characterController.velocity + movementDelta;

        Vector3 currentDrag = newVelocity.normalized * drag * Time.deltaTime;
        newVelocity = (newVelocity.magnitude > drag * Time.deltaTime) ? newVelocity - currentDrag : Vector3.zero;
        newVelocity = Vector3.ClampMagnitude(newVelocity, runSpeed);

        _characterController.Move(newVelocity * Time.deltaTime);

        // Added: after moving locally, tell the server our new
        // position and rotation so it can broadcast to other clients.
        UpdatePositionServerRpc(transform.position, transform.rotation);
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        _cameraRotation.x += lookSenseH * _playerMovement.LookInput.x;
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y - lookSenseV * _playerMovement.LookInput.y, -lookLimitV, lookLimitV);
        _playerTargetRotation.x += lookSenseH * _playerMovement.LookInput.x;

        transform.rotation = Quaternion.Euler(0f, _playerTargetRotation.x, 0f);
        _playerCamera.transform.rotation = Quaternion.Euler(_cameraRotation.y, _cameraRotation.x, 0f);
    }

    // Added: ServerRpc — this method is called on the client (owner)
    // but RUNS on the server. The server then updates the NetworkVariables
    // which automatically replicate to all other connected clients.
    // RequireOwnership = true means only the owning client can call this.
    [ServerRpc]
    private void UpdatePositionServerRpc(Vector3 position, Quaternion rotation)
    {
        _networkPosition.Value = position;
        _networkRotation.Value = rotation;
    }

    // ─── Area Tracking Triggers ───────────────────────────────────
    // Added: SchoolArea trigger volumes on your 6 areas write directly
    // into these properties when the player walks in or out.
    // The Gossipier reads CurrentAreaName and CurrentAreaType via
    // GetComponent<PlayerController>() when its FOV spots this player.
    // No separate PlayerAreaTracker component needed.
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