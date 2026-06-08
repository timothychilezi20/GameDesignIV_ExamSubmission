using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class apayinCloneScript : NetworkBehaviour, GameInput.IPlayerMovementActions, GameInput.IVotingActions
{
    [Header("References")]
    [SerializeField] private CharacterController cloneController;
    [SerializeField] private Animator cloneAnimator;

    [SerializeField] private RuntimeAnimatorController maleController;
    [SerializeField] private RuntimeAnimatorController femaleController;

    private Camera cloneMainCam;
    private GameInput cloneControls;
    private Vector2 cloneMoveInput;

    [Header("Movement")]
    [SerializeField] private float cloneMoveSpeed = 5f;
    [SerializeField] private float cloneGravity = -20f;
    [SerializeField] private float cloneJumpForce = 8f;

    [Header("Smoothing")]
    [SerializeField] private float cloneAcceleration = 10f;

    private float cloneVerticalVelocity;
    private Vector3 cloneCurrentVelocity;
    private Transform cloneCamTransform;

    private bool cloneJumpPressed;
    private float lastAnimSpeed = -1f;
    private bool lastGroundedState = true;

    private BallotCollector _ballotCollector;

    [SerializeField] private float _cliqueInteractionVisibleDuration = 4f;
    private float _cliqueInteractionTimer = 0f;

    public bool IsCollectingFromClique { get; private set; } = false;
    public string CurrentCliqueName { get; private set; } = "";
    public bool IsAtVotingStation { get; private set; } = false;


    private bool _animationTriggered = false;


    // ─────────────────────────────────────────
    // NETWORK TRANSFORM VARIABLES
    // ─────────────────────────────────────────

    private NetworkVariable<Vector3> cloneNetworkPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<Quaternion> cloneNetworkRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ─────────────────────────────────────────
    // NETWORK ANIMATION VARIABLES
    // ─────────────────────────────────────────

    private NetworkVariable<float> cloneAnimSpeed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> cloneIsGrounded = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _clonePickUpTrigger = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
);
    private NetworkVariable<bool> _cloneDropOffTrigger = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );


    // ─────────────────────────────────────────

    private bool cloneHasReceivedFirstPosition = false;

    // ─────────────────────────────────────────
    // AREA TRACKING
    // ─────────────────────────────────────────

    public string CloneCurrentAreaName { get; private set; } = "Unknown Area";
    public SchoolArea.AreaType CloneCurrentAreaType { get; private set; }
    public bool CloneIsInArea { get; private set; } = false;

    // ─────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
       // Debug.Log($"OnNetworkSpawn — IsOwner: {IsOwner} | IsHost: {IsHost} | OwnerClientId: {OwnerClientId} | LocalClientId: {NetworkManager.Singleton.LocalClientId}");

        if (IsHost)
            cloneAnimator.runtimeAnimatorController = maleController;
        else
            cloneAnimator.runtimeAnimatorController = femaleController;

        if (!IsOwner)
        {
          //  Debug.Log($"Not owner — disabling CC | OwnerClientId: {OwnerClientId} | LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            if (cloneController != null)
                cloneController.enabled = false;

            cloneNetworkPosition.OnValueChanged += OnCloneNetworkPositionFirstReceived;
            return;
        }

        StartCoroutine(OwnerSetupRoutine());
    }

    private IEnumerator OwnerSetupRoutine()
    {
        yield return null;

        if (!IsOwner)
        {
            // Disable audio listener on non-owner cameras
            AudioListener listener = Camera.main?.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
            yield break;
        }
        // One frame wait for ownership to fully propagate
        yield return null;

        if (!IsOwner)
        {
            Debug.LogWarning("OwnerSetupRoutine — IsOwner still false after wait");
            yield break;
        }

        Debug.Log("OwnerSetupRoutine — setting up owner");

        PlayerUIManager uiManager = GetComponent<PlayerUIManager>();
        Debug.Log($"PlayerUIManager found: {uiManager != null}");

        cloneController.enabled = true;
       // Debug.Log($"CharacterController enabled: {cloneController.enabled}");

       yield return null;
       // Debug.Log($"CharacterController still enabled after frame: {cloneController.enabled}");
        _ballotCollector = GetComponent<BallotCollector>();

        cloneControls = new GameInput();
        cloneControls.Enable();
        cloneControls.Voting.Enable();
        cloneControls.Voting.SetCallbacks(this);
        cloneControls.PlayerMovement.SetCallbacks(this);

        cloneMoveInput = Vector2.zero;
        cloneVerticalVelocity = 0f;

        yield return StartCoroutine(AssignCloneCamera());
    }

    private void OnCloneNetworkPositionFirstReceived(Vector3 previous, Vector3 current)
    {
        if (cloneHasReceivedFirstPosition) return;

        transform.position = current;
        cloneHasReceivedFirstPosition = true;

        cloneNetworkPosition.OnValueChanged -= OnCloneNetworkPositionFirstReceived;
    }

    private IEnumerator AssignCloneCamera()
    {
        yield return null;

        cloneMainCam = Camera.main;

        if (cloneMainCam == null)
            yield break;

        cloneCamTransform = cloneMainCam.transform;

        ThirdPersonCamera camFollow = cloneMainCam.GetComponent<ThirdPersonCamera>();

        if (camFollow != null)
            camFollow.SetTarget(transform);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        if (cloneControls == null) return;

        cloneControls.PlayerMovement.RemoveCallbacks(this);
        cloneControls.Voting.RemoveCallbacks(this);
        cloneControls.Disable();
    }

    // ─────────────────────────────────────────
    // INPUT
    // ─────────────────────────────────────────

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        cloneMoveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.performed)
            cloneJumpPressed = true;
    }

    public void OnLook(InputAction.CallbackContext context) { }

    public void OnPause(InputAction.CallbackContext context) { }

    // ─────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────

    private void Update()
    {

        // Temporary — remove after debugging
        if (IsOwner && cloneController != null && !cloneController.enabled)
        {
            Debug.LogWarning("CharacterController was disabled — re-enabling and logging stack trace");
            Debug.LogWarning(System.Environment.StackTrace);
            cloneController.enabled = true;
        }

        if (IsOwner)
        {
            HandleCloneMovement();

            if (_ballotCollector != null)
            {
                bool currentlyCollecting = _ballotCollector.IsCurrentlyCollecting;
                string currentClique = _ballotCollector.CurrentCollectingClique;

                // If actively collecting, reset the timer and store the clique name
                if (currentlyCollecting)
                {
                    _cliqueInteractionTimer = _cliqueInteractionVisibleDuration;
                    CurrentCliqueName = currentClique;
                    IsCollectingFromClique = true;
                }
                else if (_cliqueInteractionTimer > 0f)
                {
                    // Keep IsCollectingFromClique true for the duration
                    // even after the hold completes so gossipiers can catch it
                    _cliqueInteractionTimer -= Time.deltaTime;
                    IsCollectingFromClique = true;
                    // CurrentCliqueName stays as whatever was last set
                }
                else
                {
                    IsCollectingFromClique = false;
                    CurrentCliqueName = "";
                }
            }
        }
        else
        {
            if (!cloneHasReceivedFirstPosition)
                return;

            // POSITION INTERPOLATION

            transform.position = Vector3.Lerp(
                transform.position,
                cloneNetworkPosition.Value,
                Time.deltaTime * 15f
            );

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                cloneNetworkRotation.Value,
                Time.deltaTime * 15f
            );

            // REMOTE PLAYER ANIMATION

            cloneAnimator.SetFloat(
     "Speed",
     cloneAnimSpeed.Value,
     0.1f,
     Time.deltaTime
 );
            cloneAnimator.SetBool("IsGrounded", cloneIsGrounded.Value);
        }
    }

    // ─────────────────────────────────────────
    // MOVEMENT
    // ─────────────────────────────────────────

    private void HandleCloneMovement()
    {
        if (cloneCamTransform == null)
            return;

        Vector3 forward = cloneCamTransform.forward;
        Vector3 right = cloneCamTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 inputDirection =
            forward * cloneMoveInput.y +
            right * cloneMoveInput.x;

        if (inputDirection.magnitude > 1f)
            inputDirection.Normalize();

        // ROTATION

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

        // GROUND CHECK

        if (cloneController.isGrounded)
        {
            if (cloneVerticalVelocity < 0f)
                cloneVerticalVelocity = -2f;

            // JUMP

            if (cloneJumpPressed)
            {
                cloneVerticalVelocity = cloneJumpForce;
                cloneJumpPressed = false;
            }
        }

        // GRAVITY

        cloneVerticalVelocity += cloneGravity * Time.deltaTime;

        // MOVEMENT

        Vector3 targetVelocity = inputDirection * cloneMoveSpeed;

        Vector3 horizontal = new Vector3(
            cloneCurrentVelocity.x,
            0f,
            cloneCurrentVelocity.z
        );

        horizontal = Vector3.Lerp(
            horizontal,
            targetVelocity,
            cloneAcceleration * Time.deltaTime
        );

        cloneCurrentVelocity = new Vector3(
            horizontal.x,
            cloneVerticalVelocity,
            horizontal.z
        );

        cloneController.Move(cloneCurrentVelocity * Time.deltaTime);

        // ─────────────────────────────────────────
        // LOCAL ANIMATION
        // ─────────────────────────────────────────

        float animationSpeed = cloneMoveInput.magnitude;

        animationSpeed = Mathf.Clamp01(animationSpeed);

        if (animationSpeed < 0.15f)
        {
            animationSpeed = 0f;
        }

        cloneAnimator.SetFloat(
            "Speed",
            animationSpeed,
            0.1f,
            Time.deltaTime
        );

        cloneAnimator.SetBool(
            "IsGrounded",
            cloneController.isGrounded
        );

        // ─────────────────────────────────────────
        // NETWORK UPDATES
        // ─────────────────────────────────────────

        UpdateCloneTransformServerRpc(
            transform.position,
            transform.rotation
        );

        if (!Mathf.Approximately(animationSpeed, lastAnimSpeed) ||
    cloneController.isGrounded != lastGroundedState)
        {
            UpdateCloneAnimationServerRpc(
                animationSpeed,
                cloneController.isGrounded
            );

            lastAnimSpeed = animationSpeed;
            lastGroundedState = cloneController.isGrounded;
        }
    }

    // ─────────────────────────────────────────
    // SERVER RPCS
    // ─────────────────────────────────────────

    [ServerRpc]
    private void UpdateCloneTransformServerRpc(
        Vector3 position,
        Quaternion rotation
    )
    {
        cloneNetworkPosition.Value = position;
        cloneNetworkRotation.Value = rotation;
    }

    [ServerRpc]
    private void UpdateCloneAnimationServerRpc(
        float speed,
        bool grounded
    )
    {
        cloneAnimSpeed.Value = speed;
        cloneIsGrounded.Value = grounded;
    }

    // ─────────────────────────────────────────
    // AREA TRACKING
    // ─────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {

       // Debug.Log($"OnTriggerEnter — other: {other.gameObject.name} | IsOwner: {IsOwner} | layer: {other.gameObject.layer}");
        SchoolArea area = other.GetComponent<SchoolArea>();
        if (area != null)
        {
            CloneCurrentAreaName = area.areaName;
            CloneCurrentAreaType = area.areaType;
            CloneIsInArea = true;
        }

        CliqueGroup group = other.GetComponent<CliqueGroup>();
        if (group != null)
            group.SetNearbyPlayer(transform);

        VotingStation station = other.GetComponent<VotingStation>();
        if (station != null)
        {
            _ballotCollector.SetCurrentStation(station);
            IsAtVotingStation = true;
        }
           
        

    }

    private void OnTriggerExit(Collider other)
    {
        SchoolArea area = other.GetComponent<SchoolArea>();
        if (area != null)
        {
            CloneIsInArea = false;
            CloneCurrentAreaName = "Unknown Area";
        }

        CliqueGroup group = other.GetComponent<CliqueGroup>();
        if (group != null)
            group.ClearNearbyPlayer();

        VotingStation station = other.GetComponent<VotingStation>();
        if (station != null)
        {
            _ballotCollector.ClearCurrentStation();
            IsAtVotingStation = false;
        }
          
    }

    public void OnCollectVotes(InputAction.CallbackContext context)
    {
        if (!IsOwner || _ballotCollector == null) return;

        if (_ballotCollector.CurrentStation != null)
        {
            if (context.started)
            {
                Debug.Log("[apayinCloneScript] LockInVotes triggered");

                // Only play dropoff animation if there are ballots to dump
                if (_ballotCollector.GetBallotCount() > 0)
                {
                    cloneAnimator.SetTrigger("DropOffTrigger");
                    TriggerDropOffServerRpc();
                }

                _ballotCollector.LockInVotes();
            }
            return;
        }

        if (context.started)
        {
            // Guard prevents animation from firing multiple times
            if (!_animationTriggered)
            {
                _animationTriggered = true;
                cloneAnimator.SetTrigger("PickUpTrigger");
                TriggerPickUpServerRpc();
            }
            _ballotCollector.OnCollectVotesStarted();
        }
        else if (context.performed)
        {
            _ballotCollector.OnCollectVotesPerformed();
            cloneAnimator.SetTrigger("PickUpTrigger");
            TriggerPickUpServerRpc();
        }
        else if (context.canceled)
        {
            _ballotCollector.OnCollectVotesCancelled();

            _animationTriggered = false;
            cloneAnimator.ResetTrigger("PickUpTrigger");
        }
    }

    public void OnDumpVotes(InputAction.CallbackContext context)
    {
        if (!IsOwner || _ballotCollector == null) return;

        if (context.performed)
        {
            // _ballotCollector.TryDumpBallots();

            // Play dropoff animation locally and sync to others
            cloneAnimator.SetTrigger("DropOffTrigger");
            TriggerDropOffServerRpc();
        }
    }

    [ServerRpc]
    private void TriggerPickUpServerRpc()
    {
        TriggerPickUpClientRpc();
    }

    [ServerRpc]
    private void TriggerDropOffServerRpc()
    {
        TriggerDropOffClientRpc();
    }

    [ClientRpc]
    private void TriggerPickUpClientRpc()
    {
        if (IsOwner) return; // owner already played it locally
        cloneAnimator.SetTrigger("PickUpTrigger");
    }

    [ClientRpc]
    private void TriggerDropOffClientRpc()
    {
        if (IsOwner) return;
        cloneAnimator.SetTrigger("DropOffTrigger");
    }
}
