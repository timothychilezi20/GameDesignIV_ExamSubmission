using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Gossipier : MonoBehaviour
{
    // Map
    [Header("Map Identity")]
    [SerializeField] private int _mapPlayerNumber = 1; // set to 1 or 2 in Inspector

    // ─── Routes ───────────────────────────────────────────────────
    [Header("Routes")]
    [Tooltip("Assign 3 route parents. Each parent's children are its waypoints.")]
    [SerializeField] private Transform[] _routes; // 3 route parent objects

    // ─── Movement ─────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _waypointReachedDistance = 0.2f;

    // ─── Scanning (Spin) ──────────────────────────────────────────
    [Header("Scanning")]
    [SerializeField] private float _spinSpeed = 90f;      // degrees per second
    [SerializeField] private float _spinDuration = 2f;    // seconds spent spinning

    // ─── FOV ──────────────────────────────────────────────────────
    [Header("FOV")]
    [SerializeField] private FieldOfView _fieldOfView;
    [SerializeField] private LayerMask _playerMask;
    [SerializeField] private float _detectionInterval = 0.2f; // how often to scan for player

    // ─── Internal state ───────────────────────────────────────────
    private Transform[] _currentWaypoints;   // waypoints of chosen route
    private int _currentWaypointIndex = 0;
    private HashSet<int> _spinWaypointIndices = new HashSet<int>(); // which 4 waypoints trigger a spin
    private bool _isSpinning = false;
    private SchoolArea _currentArea = null;  // area the gossipier is currently inside

    // ──────────────────────────────────────────────────────────────

    private void Start()
    {
        PickRandomRoute();
        StartCoroutine(DetectionRoutine());
    }

    // ─── Route Setup ──────────────────────────────────────────────

    private void PickRandomRoute()
    {
        if (_routes == null || _routes.Length == 0)
        {
            Debug.LogWarning("Gossipier: No routes assigned.");
            return;
        }

        // Pick a random route
        Transform chosenRoute = _routes[Random.Range(0, _routes.Length)];

        // Collect all waypoints from that route's children
        int count = chosenRoute.childCount;
        _currentWaypoints = new Transform[count];
        for (int i = 0; i < count; i++)
            _currentWaypoints[i] = chosenRoute.GetChild(i);

        // Spawn at first waypoint
        transform.position = _currentWaypoints[0].position;
        _currentWaypointIndex = 0;

        // Pick 4 random waypoint indices (excluding index 0 — just spawned there)
        PickSpinWaypoints();
    }

    private void PickSpinWaypoints()
    {
        _spinWaypointIndices.Clear();

        // Build a pool of all indices except 0
        List<int> pool = new List<int>();
        for (int i = 1; i < _currentWaypoints.Length; i++)
            pool.Add(i);

        // Shuffle and take up to 4
        int spinCount = Mathf.Min(4, pool.Count);
        for (int i = 0; i < spinCount; i++)
        {
            int randomIndex = Random.Range(i, pool.Count);
            (pool[i], pool[randomIndex]) = (pool[randomIndex], pool[i]); // swap
            _spinWaypointIndices.Add(pool[i]);
        }
    }

    // ─── Movement & Patrol ────────────────────────────────────────

    private void Update()
    {
        if (_isSpinning || _currentWaypoints == null || _currentWaypoints.Length == 0)
            return;

        MoveTowardsWaypoint();
    }

    private void MoveTowardsWaypoint()
    {
        Transform target = _currentWaypoints[_currentWaypointIndex];
        Vector3 direction = (target.position - transform.position);
        direction.y = 0f;

        float distance = direction.magnitude;

        if (direction != Vector3.zero)
        {
            // Changed: Slerp → RotateTowards
            // Slerp is frame-rate dependent and causes the gossipier to
            // lag behind its direction of travel, especially at low speeds.
            // RotateTowards rotates at a fixed degrees-per-second rate so
            // the gossipier snaps to face the waypoint cleanly and consistently.
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                360f * Time.deltaTime // full rotation possible within 1 second
            );
        }

        transform.position += direction.normalized * _moveSpeed * Time.deltaTime;

        if (distance <= _waypointReachedDistance)
            OnWaypointReached(_currentWaypointIndex);
    }
    private void OnWaypointReached(int index)
    {
        // Snap to waypoint cleanly
        transform.position = _currentWaypoints[index].position;

        // Check if this is a spin waypoint
        if (_spinWaypointIndices.Contains(index))
        {
            StartCoroutine(SpinRoutine());
        }
        else
        {
            AdvanceWaypoint();
        }
    }

    private void AdvanceWaypoint()
    {
        _currentWaypointIndex++;

        // Loop back to start when route is complete
        if (_currentWaypointIndex >= _currentWaypoints.Length)
            _currentWaypointIndex = 0;
    }

    // ─── Spinning / Scanning ──────────────────────────────────────

    private IEnumerator SpinRoutine()
    {
        _isSpinning = true;

        float elapsed = 0f;
        while (elapsed < _spinDuration)
        {
            transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _isSpinning = false;
        AdvanceWaypoint();
    }

    // ─── Player Detection ─────────────────────────────────────────

    // Runs on a interval rather than every frame for performance
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
        // Cast a sphere at the gossipier's position to find any players
        // within the view distance, then verify they're within the FOV angle
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _fieldOfView.viewDistance,
            _playerMask
        );

        foreach (Collider hit in hits)
        {
            Vector3 directionToPlayer = (hit.transform.position - transform.position).normalized;
            float angleTo = Vector3.Angle(transform.forward, directionToPlayer);

            // Check if player is within the FOV cone angle
            if (angleTo > _fieldOfView.fov / 2f) continue;

            // Check line of sight — make sure no obstacle is between gossipier and player
            if (Physics.Raycast(
                transform.position,
                directionToPlayer,
                out RaycastHit rayHit,
                _fieldOfView.viewDistance,
                _fieldOfView.obstacleMask))
            {
                // Something is blocking the view — not detected
                continue;
            }

            // Player is visible — log based on area type
            LogPlayerSpotted(hit.transform);
        }
    }

    private void LogPlayerSpotted(Transform player)
    {
        apayinCloneScript playerController = player.GetComponent<apayinCloneScript>();
        PlayerUIManager playerUI = player.GetComponent<PlayerUIManager>();
        int playerNumber = playerUI != null ? playerUI.GetPlayerNumber() : 0;

        if (playerNumber == 0)
            playerNumber = _mapPlayerNumber;

        string playerLabel = $"Player {playerNumber}";
        string message = "";

        // Priority 1: player is actively collecting from a clique
        // This is the most specific and interesting state to report
        if (playerController != null && playerController.IsCollectingFromClique)
        {
            message = $"{playerLabel} seen talking to {playerController.CurrentCliqueName}";
        }
        // Priority 2: player is at a voting station
        else if (playerController != null && playerController.IsAtVotingStation)
        {
            message = $"{playerLabel} seen at the voting station";
        }
        // Priority 3: player is in a named area
        else if (playerController != null && playerController.CloneIsInArea)
        {
            string areaName = playerController.CloneCurrentAreaName;
            bool isInterior = playerController.CloneCurrentAreaType == SchoolArea.AreaType.Interior;
            message = isInterior
                ? $"{playerLabel} seen inside {areaName}"
                : $"{playerLabel} seen at {areaName}";
        }
        // Priority 4: player is in the hallway
        else
        {
            message = $"{playerLabel} spotted in hallway";
        }

        Debug.Log(message);

        // Send to rumor feed via GossipRelay
        GossipRelay[] relays = FindObjectsByType<GossipRelay>(FindObjectsSortMode.None);
        foreach (GossipRelay relay in relays)
        {
            if (!relay.IsOwner) continue;
            relay.SendDirectReport(message, playerNumber);
            break;
        }
    }

    // ─── Area Tracking ────────────────────────────────────────────
    // SchoolArea trigger volumes tell the gossipier which area it's in.
    // The gossipier uses this to know what to report when it spots a player.

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
}