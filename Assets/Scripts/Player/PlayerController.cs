using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

// Added: IPlayerVotingActions to the interface list
// so the Voting action map callbacks wire up correctly
public class PlayerController : NetworkBehaviour,
    PlayerControls.IPlayerMovementActions,
    PlayerControls.IVotingActions
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

    // Added: reference to BallotCollector on the same prefab
    private BallotCollector _ballotCollector;

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

    public string CurrentAreaName { get; private set; } = "Unknown Area";
    public SchoolArea.AreaType CurrentAreaType { get; private set; }
    public bool IsInArea { get; private set; } = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (controller != null)
                controller.enabled = false;
            return;
        }

        controller.enabled = true;

        // Added: grab BallotCollector on spawn
        _ballotCollector = GetComponent<BallotCollector>();

        controls = new PlayerControls();
        controls.Enable();
        controls.PlayerMovement.SetCallbacks(this);

        // Added: register Voting action map callbacks
        controls.Voting.Enable();
        controls.Voting.SetCallbacks(this);

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
            camFollow.SetTarget(transform);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        controls.PlayerMovement.RemoveCallbacks(this);

        // Added: clean up Voting callbacks on despawn
        controls.Voting.RemoveCallbacks(this);
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

    // Added: Voting action map callback
    // The Hold interaction fires three phases:
    // Started  = player began holding E
    // Performed = hold duration completed successfully
    // Canceled  = player released E before hold completed
    public void OnCollectVotes(InputAction.CallbackContext context)
    {
        if (!IsOwner || _ballotCollector == null) return;

        if (context.started)
        {
            _ballotCollector.TryDumpBallots();
            _ballotCollector.OnCollectVotesStarted();
        }
            
        else if (context.performed)
            _ballotCollector.OnCollectVotesPerformed();
        else if (context.canceled)
            _ballotCollector.OnCollectVotesCancelled();
    }

    // ---------------- UPDATE ----------------

    private void Update()
    {
        if (IsOwner)
        {
            HandleMovement();
        }
        else
        {
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

        Vector3 inputDirection = forward * moveInput.y + right * moveInput.x;

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

        horizontal = Vector3.Lerp(horizontal, targetVelocity, acceleration * Time.deltaTime);

        currentVelocity = new Vector3(horizontal.x, verticalVelocity, horizontal.z);

        controller.Move(currentVelocity * Time.deltaTime);
        UpdateTransformServerRpc(transform.position, transform.rotation);
    }

    [ServerRpc]
    private void UpdateTransformServerRpc(Vector3 position, Quaternion rotation)
    {
        _networkPosition.Value = position;
        _networkRotation.Value = rotation;
    }

    // Added: CliqueGroup proximity tracking via trigger
    // The player's trigger CapsuleCollider detects when it
    // overlaps a Group object and tells it a player is nearby
    // so members can start rotating to face the player
    private void OnTriggerEnter(Collider other)
    {
        SchoolArea area = other.GetComponent<SchoolArea>();
        if (area != null)
        {
            CurrentAreaName = area.areaName;
            CurrentAreaType = area.areaType;
            IsInArea = true;
        }

        CliqueGroup group = other.GetComponent<CliqueGroup>();
        if (group != null)
            group.SetNearbyPlayer(transform);

        VotingStation station = other.GetComponent<VotingStation>();
        if (station != null)
            _ballotCollector.SetCurrentStation(station);

    }

    private void OnTriggerExit(Collider other)
    {
        SchoolArea area = other.GetComponent<SchoolArea>();
        if (area != null)
        {
            IsInArea = false;
            CurrentAreaName = "Unknown Area";
        }

        CliqueGroup group = other.GetComponent<CliqueGroup>();
        if (group != null)
            group.ClearNearbyPlayer();

        VotingStation station = other.GetComponent<VotingStation>();
        if (station != null)
            _ballotCollector.ClearCurrentStation();
    }
}