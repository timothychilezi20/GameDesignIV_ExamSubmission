using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class BallotCollector : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _maxBallots = 10;
    [SerializeField] private float _interactCheckRadius = 3f;
    [SerializeField] private LayerMask _cliqueLayer;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _ballotText;

    private bool _hasLockedIn = false;
    public bool HasLockedIn => _hasLockedIn;

    public VotingStation CurrentStation => _currentStation;
    public bool IsCurrentlyCollecting => _isCollecting;
    public string CurrentCollectingClique => _isCollecting && _currentGroup != null
        ? _currentGroup.cliqueType.ToString()
        : "";

    private NetworkVariable<int> _ballotCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<int> _artistBallots = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );
    private NetworkVariable<int> _nerdBallots = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );
    private NetworkVariable<int> _athleteBallots = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    private CliqueGroup _currentGroup = null;
    private bool _isCollecting = false;
    private VotingStation _currentStation = null;

    public override void OnNetworkSpawn()
    {
        _ballotCount.OnValueChanged += OnBallotCountChanged;

        if (_ballotText != null)
            UpdateBallotText(_ballotCount.Value);

        // If text isn't set yet, retry after a frame
        if (_ballotText == null && IsOwner)
            StartCoroutine(RetrySetBallotText());
    }

    private IEnumerator RetrySetBallotText()
    {
        yield return null;

        if (_ballotText == null)
        {
            PlayerUIManager uiManager = GetComponent<PlayerUIManager>();
            if (uiManager != null)
                uiManager.ForceApplyUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        _ballotCount.OnValueChanged -= OnBallotCountChanged;
    }

    public void SetCurrentStation(VotingStation station) => _currentStation = station;
    public void ClearCurrentStation() => _currentStation = null;

    public void ClearBallots()
    {
        if (!IsOwner) return;
        _ballotCount.Value = 0;
        _artistBallots.Value = 0;
        _nerdBallots.Value = 0;
        _athleteBallots.Value = 0;
        Debug.Log("Ballots cleared");
    }

    [ServerRpc]
   
    private void AddVotesToServerRpc(int artists, int nerds, int athletes)
    {
        int playerNumber = OwnerClientId == 0 ? 1 : 2;

        // Record in VoteManager for overall totals
        VoteManager.Instance.ReceiveVotes(artists, nerds, athletes);

        // Record in RoundStats for per-round per-player breakdown
        if (RoundStats.Instance != null)
            RoundStats.Instance.RecordBallots(playerNumber, artists, nerds, athletes);
    }

    public void DumpBallotsToServer()
    {
        if (!IsOwner) return;
        if (GetBallotCount() == 0) return;

        int artists = GetArtistBallots();
        int nerds = GetNerdBallots();
        int athletes = GetAthleteBallots();

        ClearBallots();
        AddVotesToServerRpc(artists, nerds, athletes);
    }

    public void OnCollectVotesStarted()
    {
        if (!IsOwner) return;

        // Hard reset collecting state before each new attempt
        // prevents stale state from blocking subsequent holds
        _isCollecting = false;
        _currentGroup = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, _interactCheckRadius, _cliqueLayer);
        if (hits.Length == 0) return;

        float closest = float.MaxValue;
        CliqueGroup closestGroup = null;

        foreach (Collider hit in hits)
        {
            CliqueGroup group = hit.GetComponent<CliqueGroup>();
            if (group == null || group.HasBeenCollected) continue;
            if (group._classInSession) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < closest)
            {
                closest = dist;
                closestGroup = group;
            }
        }

        if (closestGroup == null) return;

        float distanceToClosest = Vector3.Distance(transform.position, closestGroup.transform.position);
        Debug.Log($"Closest group: {closestGroup.gameObject.name} | Distance: {distanceToClosest} | Interact distance needed: {closestGroup.InteractDistance}");

        if (!closestGroup.IsPlayerInInteractRange(transform.position)) return;

        _currentGroup = closestGroup;
        _isCollecting = true;
    }

    public void OnCollectVotesPerformed()
    {
        if (!IsOwner || !_isCollecting || _currentGroup == null) return;

        int ballotsToAdd = _currentGroup.GetMemberCount();
        int newCount = Mathf.Min(_ballotCount.Value + ballotsToAdd, _maxBallots);

        switch (_currentGroup.cliqueType)
        {
            case CliqueGroup.CliqueType.Artists:
                _artistBallots.Value = Mathf.Min(_artistBallots.Value + ballotsToAdd, _maxBallots);
                break;
            case CliqueGroup.CliqueType.Nerds:
                _nerdBallots.Value = Mathf.Min(_nerdBallots.Value + ballotsToAdd, _maxBallots);
                break;
            case CliqueGroup.CliqueType.Athletes:
                _athleteBallots.Value = Mathf.Min(_athleteBallots.Value + ballotsToAdd, _maxBallots);
                break;
        }

        _ballotCount.Value = newCount;
        _currentGroup.MarkCollected();

        UpdateBallotText(newCount);

        Debug.Log($"Collected {ballotsToAdd} {_currentGroup.cliqueType} ballots | " +
                  $"Artists: {_artistBallots.Value} | Nerds: {_nerdBallots.Value} | Athletes: {_athleteBallots.Value} | " +
                  $"Total: {_ballotCount.Value}/{_maxBallots}");

        _currentGroup = null;
        _isCollecting = false;
    }

    public void OnCollectVotesCancelled()
    {
        _currentGroup = null;
        _isCollecting = false;
    }

    private void OnBallotCountChanged(int previous, int current)
    {
        UpdateBallotText(current);
    }

    private void UpdateBallotText(int count)
    {
        if (_ballotText != null)
            _ballotText.text = $"{count}/{_maxBallots}";
    }

    public void SetBallotText(TextMeshProUGUI text)
    {
        _ballotText = text;
        UpdateBallotText(_ballotCount.Value);
    }

    public void LockInVotes()
    {
        if (!IsOwner || _hasLockedIn) return;
        if (GetBallotCount() == 0 && GetArtistBallots() == 0 &&
            GetNerdBallots() == 0 && GetAthleteBallots() == 0)
        {
            Debug.Log("No votes to lock in");
            return;
        }

        DumpBallotsToServer();
        _hasLockedIn = true;
      //  Time.timeScale = 0f;
        Debug.Log("Votes locked in — waiting for other player");

        // Get player number from local client ID directly
        // OwnerClientId 0 = host = Player 1, anything else = Player 2
        int playerNumber = OwnerClientId == 0 ? 1 : 2;
        Debug.Log($"Locking in as Player {playerNumber}");

        LockInServerRpc(playerNumber);
    }

    [ServerRpc]
    private void LockInServerRpc(int playerNumber)
    {
        Debug.Log($"LockInServerRpc — playerNumber: {playerNumber} | RoundManager null: {RoundManager.Instance == null}");
        if (playerNumber == 0) return;
        RoundManager.Instance.HandleLockIn(playerNumber);
    }

    public void ResetForNewRound()
    {
        _hasLockedIn = false;
        _isCollecting = false;
      //  Time.timeScale = 1f;
        Debug.Log("BallotCollector reset for new round");
    }

    public int GetBallotCount() => _ballotCount.Value;
    public int GetArtistBallots() => _artistBallots.Value;
    public int GetNerdBallots() => _nerdBallots.Value;
    public int GetAthleteBallots() => _athleteBallots.Value;
}