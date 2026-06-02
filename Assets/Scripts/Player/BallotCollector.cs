using UnityEngine;
using Unity.Netcode;
using TMPro;

public class BallotCollector : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _maxBallots = 10;
    [SerializeField] private float _interactCheckRadius = 3f;
    [SerializeField] private LayerMask _cliqueLayer;

    [Header("UI")]
    [SerializeField] private TextMeshPro _ballotText;

    // Total ballot count — still used for the 0/10 display and max cap
    private NetworkVariable<int> _ballotCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // Added: separate tracked counts per clique type.
    // These are what get converted to categorised votes on dump.
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
        UpdateBallotText(_ballotCount.Value);
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

    public void TryDumpBallots()
    {
        if (_currentStation == null) return;
        _currentStation.TryDumpBallots(this);
    }

    public void OnCollectVotesStarted()
    {
        if (!IsOwner || _isCollecting) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, _interactCheckRadius, _cliqueLayer);
        if (hits.Length == 0) return;

        float closest = float.MaxValue;
        CliqueGroup closestGroup = null;
        foreach (Collider hit in hits)
        {
            CliqueGroup group = hit.GetComponent<CliqueGroup>();
            if (group == null || group.HasBeenCollected) continue;

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

        // Added: add to the correct clique category based on group type.
        // Each clique's ballot count is tracked separately so VoteManager
        // can store categorised votes when the player dumps at a station.
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

    public void SetBallotText(TMPro.TextMeshPro text)
    {
        _ballotText = text;
        UpdateBallotText(_ballotCount.Value);
    }

    public int GetBallotCount() => _ballotCount.Value;

    // Added: getters for each clique category so VotingStation
    // can pass categorised totals to VoteManager on dump
    public int GetArtistBallots() => _artistBallots.Value;
    public int GetNerdBallots() => _nerdBallots.Value;
    public int GetAthleteBallots() => _athleteBallots.Value;
}