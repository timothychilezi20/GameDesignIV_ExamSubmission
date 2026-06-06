using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class ClonePlayerScript : NetworkBehaviour, GameInput.IPlayerMovementActions, GameInput.IVotingActions
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
        if (IsHost)
        {
            cloneAnimator.runtimeAnimatorController = maleController;
        }
        else
        {
            cloneAnimator.runtimeAnimatorController = femaleController;
        }

        if (!IsOwner)
        {
            if (cloneController != null)
                cloneController.enabled = false;

            cloneNetworkPosition.OnValueChanged += OnCloneNetworkPositionFirstReceived;
            return;
        }

        cloneController.enabled = true;

        cloneControls = new GameInput();
        cloneControls.Enable();

        cloneControls.PlayerMovement.SetCallbacks(this);

        cloneControls.Voting.Enable();
        cloneControls.Voting.SetCallbacks(this);

        cloneMoveInput = Vector2.zero;
        cloneVerticalVelocity = 0f;

        StartCoroutine(AssignCloneCamera());
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

        cloneControls.PlayerMovement.RemoveCallbacks(this);
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
        if (IsOwner)
        {
            HandleCloneMovement();
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
           _ballotCollector.SetCurrentStation(station);

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
           _ballotCollector.ClearCurrentStation();
   }

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
}