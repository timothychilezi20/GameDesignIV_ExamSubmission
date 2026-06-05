using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GossiperClone : MonoBehaviour
{
    // Map Identity
    [Header("Map Identity")]
    [SerializeField] private int _mapPlayerNumber = 1;

    // Routes
    [Header("Routes")]
    [SerializeField] private Transform[] _routes;

    // Movement
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _waypointReachedDistance = 0.2f;

    // Animation
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _turnAnimationThreshold = 45f;
    [SerializeField] private float _animSmoothTime = 0.1f;

    // Scanning
    [Header("Scanning")]
    [SerializeField] private float _spinSpeed = 90f;
    [SerializeField] private float _spinDuration = 2f;

    // FOV
    [Header("FOV")]
    [SerializeField] private FieldOfView _fieldOfView;
    [SerializeField] private LayerMask _playerMask;
    [SerializeField] private float _detectionInterval = 0.2f;

    // Internal State
    private Transform[] _currentWaypoints;
    private int _currentWaypointIndex = 0;
    private HashSet<int> _spinWaypointIndices = new HashSet<int>();
    private bool _isSpinning = false;
    private SchoolArea _currentArea = null;

    private Vector3 _lastPosition;
    private float _currentAnimSpeed;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_animator == null)
            Debug.LogWarning($"{gameObject.name} is missing an Animator component!");
    }

    private void Start()
    {
        PickRandomRoute();
        StartCoroutine(DetectionRoutine());
    }

    private void LateUpdate()
    {
        float velocity = Vector3.Distance(transform.position, _lastPosition) / Time.deltaTime;
        float normalizedSpeed = Mathf.InverseLerp(0f, _moveSpeed, velocity);

        _currentAnimSpeed = Mathf.Lerp(_currentAnimSpeed, normalizedSpeed, Time.deltaTime * 8f);
        _animator.SetFloat("Speed", _currentAnimSpeed);

        _lastPosition = transform.position;
    }

    // =========================
    // ANIMATION (Blend Tree)
    // =========================

    private void SetScanning(bool value)
    {
        if (_animator != null)
            _animator.SetBool("IsScanning", value);
    }

    private void TriggerTurn()
    {
        if (_animator != null)
            _animator.SetTrigger("Turn");
    }

    // =========================
    // ROUTE SETUP
    // =========================

    private void PickRandomRoute()
    {
        if (_routes == null || _routes.Length == 0)
        {
            Debug.LogWarning("Gossipier: No routes assigned.");
            return;
        }

        Transform chosenRoute = _routes[Random.Range(0, _routes.Length)];

        int count = chosenRoute.childCount;
        _currentWaypoints = new Transform[count];

        for (int i = 0; i < count; i++)
            _currentWaypoints[i] = chosenRoute.GetChild(i);

        transform.position = _currentWaypoints[0].position;
        _currentWaypointIndex = 0;

        PickSpinWaypoints();
    }

    private void PickSpinWaypoints()
    {
        _spinWaypointIndices.Clear();

        List<int> pool = new List<int>();

        for (int i = 1; i < _currentWaypoints.Length; i++)
            pool.Add(i);

        int spinCount = Mathf.Min(4, pool.Count);

        for (int i = 0; i < spinCount; i++)
        {
            int randomIndex = Random.Range(i, pool.Count);
            (pool[i], pool[randomIndex]) = (pool[randomIndex], pool[i]);
            _spinWaypointIndices.Add(pool[i]);
        }
    }

    // =========================
    // UPDATE LOOP
    // =========================

    private void Update()
    {
        if (_isSpinning || _currentWaypoints == null || _currentWaypoints.Length == 0)
        {
            return;
        }

        MoveTowardsWaypoint();
    }

    private void MoveTowardsWaypoint()
    {
        Transform target = _currentWaypoints[_currentWaypointIndex];

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance > _waypointReachedDistance)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                360f * Time.deltaTime
            );

            // Move at full speed — no speedMultiplier
            transform.position += direction.normalized * _moveSpeed * Time.deltaTime;
        }

        if (distance <= _waypointReachedDistance)
            OnWaypointReached(_currentWaypointIndex);
    }

    // =========================
    // WAYPOINT LOGIC
    // =========================

    private void OnWaypointReached(int index)
    {
        transform.position = _currentWaypoints[index].position;

        int nextIndex = (index + 1) % _currentWaypoints.Length;
        Vector3 nextDirection = (_currentWaypoints[nextIndex].position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, nextDirection);

        if (_spinWaypointIndices.Contains(index))
        {
            // Spin waypoints: turn first (if needed), then scan
            if (angle > _turnAnimationThreshold)
                StartCoroutine(TurnThenSpin(nextDirection));
            else
                StartCoroutine(SpinRoutine());
        }
        else
        {
            // Normal waypoints: turn first (if needed), then walk
            if (angle > _turnAnimationThreshold)
                StartCoroutine(TurnThenAdvance(nextDirection));
            else
                AdvanceWaypoint();
        }
    }

    private void AdvanceWaypoint()
    {
        _currentWaypointIndex++;

        if (_currentWaypointIndex >= _currentWaypoints.Length)
            _currentWaypointIndex = 0;
    }

    // =========================
    // SCANNING
    // =========================

    private IEnumerator SpinRoutine()
    {
        _isSpinning = true; // still set it here for when called standalone

        SetScanning(true);

        float elapsed = 0f;
        while (elapsed < _spinDuration)
        {
            transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetScanning(false);
        _isSpinning = false;
        AdvanceWaypoint();
    }

    // =========================
    // PLAYER DETECTION
    // =========================

    private IEnumerator DetectionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_detectionInterval);
            CheckForPlayer();
        }
    }

    private void CheckForPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _fieldOfView.viewDistance,
            _playerMask
        );

        foreach (Collider hit in hits)
        {
            Vector3 directionToPlayer =
                (hit.transform.position - transform.position).normalized;

            float angleTo =
                Vector3.Angle(transform.forward, directionToPlayer);

            if (angleTo > _fieldOfView.fov / 2f)
                continue;

            if (Physics.Raycast(
                transform.position,
                directionToPlayer,
                out RaycastHit rayHit,
                _fieldOfView.viewDistance,
                _fieldOfView.obstacleMask))
            {
                continue;
            }

            LogPlayerSpotted(hit.transform);
        }
    }

    private void LogPlayerSpotted(Transform player)
    {
        apayinCloneScript playerController =
            player.GetComponent<apayinCloneScript>();

        PlayerUIManager playerUI =
            player.GetComponent<PlayerUIManager>();

        int playerNumber =
            playerUI != null ? playerUI.GetPlayerNumber() : 0;

        string playerLabel =
            playerNumber > 0 ? $"Player {playerNumber}" : "A player";

        if (playerController == null || !playerController.CloneIsInArea)
        {
            Debug.Log($"{playerLabel} spotted in hallway (Map {_mapPlayerNumber})");
            return;
        }

        string areaName = playerController.CloneCurrentAreaName;
        bool isInterior =
            playerController.CloneCurrentAreaType == SchoolArea.AreaType.Interior;

        Debug.Log($"{playerLabel} seen {(isInterior ? "inside" : "at")} {areaName}");

        if (playerNumber > 0 && RumorManager.Instance != null)
        {
            RumorManager.Instance.ReportSpottingServerRpc(
                playerNumber,
                areaName,
                isInterior
            );
        }
    }

    // =========================
    // AREA TRACKING
    // =========================

    private void OnTriggerEnter(Collider other)
    {
        SchoolArea area = other.GetComponent<SchoolArea>();

        if (area != null)
            _currentArea = area;
    }

    private void OnTriggerExit(Collider other)
    {
        SchoolArea area = other.GetComponent<SchoolArea>();

        if (area != null && _currentArea == area)
            _currentArea = null;
    }

    private IEnumerator TurnThenAdvance(Vector3 targetDirection)
    {
        _isSpinning = true; // reuse this flag to pause movement

        TriggerTurn();
        yield return StartCoroutine(RotateToDirection(targetDirection));

        _isSpinning = false;
        AdvanceWaypoint();
    }

    private IEnumerator TurnThenSpin(Vector3 targetDirection)
    {
        _isSpinning = true;

        TriggerTurn();
        yield return StartCoroutine(RotateToDirection(targetDirection));

        yield return StartCoroutine(SpinRoutine());
    }

    private IEnumerator RotateToDirection(Vector3 targetDirection)
    {
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        yield return null; // wait one frame for trigger to register

        Debug.Log($"[Turn] Waiting to enter Turn state. Current: {_animator.GetCurrentAnimatorStateInfo(0).fullPathHash}");

        // Timeout safety net
        float timeout = 2f;
        float elapsed = 0f;

        // Wait until we're in the Turning state
        while (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Turning"))
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning("[Turn] Timed out waiting to ENTER Turning state. Skipping.");
                transform.rotation = targetRotation;
                yield break;
            }
            yield return null;
        }

        Debug.Log("[Turn] Entered Turning state.");
        elapsed = 0f;

        // Wait until it finishes
        while (true)
        {
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            elapsed += Time.deltaTime;

            Debug.Log($"[Turn] normalizedTime: {info.normalizedTime:F2}, isName: {info.IsName("Turning")}");

            if (info.IsName("Turning") && info.normalizedTime >= 0.95f)
                break;

            if (elapsed >= timeout)
            {
                Debug.LogWarning("[Turn] Timed out waiting for Turning to FINISH. Skipping.");
                break;
            }

            yield return null;
        }

        Debug.Log("[Turn] Turn complete.");
        transform.rotation = targetRotation;
    }
}