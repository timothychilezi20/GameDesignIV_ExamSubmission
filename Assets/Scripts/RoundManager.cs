using UnityEngine;
using Unity.Netcode;
using System.Collections;

// Singleton NetworkBehaviour managing the round structure.
// Tracks which players have locked in, pauses both games
// when both are locked, shows the reveal panel, then resumes.
// Place in scene alongside VoteManager and RumorManager.
public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Reveal Settings")]
    [SerializeField] private float _revealDuration = 30f;
    [SerializeField] private int _totalRounds = 3; //Keeps track of the total rounds

    // Tracks lock-in state for each player — server writes, all read
    private NetworkVariable<bool> _player1LockedIn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _player2LockedIn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _revealActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _currentRound = new NetworkVariable<int>( 1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server );

    // Per-round vote snapshots — 3 rounds, 2 players
    // Index: [round 0-2][player 0-1]
    private int[,] _roundVotes = new int[3, 4];

    // Track votes at start of each round to calculate delta
    private int _player1VotesAtRoundStart = 0;
    private int _player2VotesAtRoundStart = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to reveal state changes so all clients
        // can show/hide the reveal panel reactively
        _revealActive.OnValueChanged += OnRevealStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _revealActive.OnValueChanged -= OnRevealStateChanged;
    }

    // Called by BallotCollector when a player finishes the lock-in hold
    [ServerRpc]
    public void LockInVotesServerRpc(int playerNumber)
    {

        Debug.Log($"LockInVotesServerRpc received — playerNumber: {playerNumber}");
        if (playerNumber == 1)
            _player1LockedIn.Value = true;
        else if (playerNumber == 2)
            _player2LockedIn.Value = true;

        Debug.Log($"Lock state — P1: {_player1LockedIn.Value} | P2: {_player2LockedIn.Value}");

        // Notify the other player via rumor feed
        NotifyLockInClientRpc(playerNumber);

        // Check if both players have locked in
        if (_player1LockedIn.Value && _player2LockedIn.Value)
        {
            Debug.Log("Both locked in — starting reveal");
            StartCoroutine(RevealSequence());
        }
    }

    private void SnapshotRoundVotes()
    {
        Debug.Log($"[SnapshotRoundVotes] P1 total: {VoteManager.Instance.GetPlayer1Votes()} | P2 total: {VoteManager.Instance.GetPlayer2Votes()}");
        Debug.Log($"[SnapshotRoundVotes] P1 start: {_player1VotesAtRoundStart} | P2 start: {_player2VotesAtRoundStart}");

        if (VoteManager.Instance == null) return;

        int round = _currentRound.Value - 1;

        int p1Total = VoteManager.Instance.GetPlayer1Votes();
        int p2Total = VoteManager.Instance.GetPlayer2Votes();

        _roundVotes[round, 0] = p1Total - _player1VotesAtRoundStart;
        _roundVotes[round, 1] = p2Total - _player2VotesAtRoundStart;

        _player1VotesAtRoundStart = p1Total;
        _player2VotesAtRoundStart = p2Total;

        // Get rival scores split between two rivals
        int rival1Score = 0;
        int rival2Score = 0;
        RivalCoupleTimer rivalTimer = FindFirstObjectByType<RivalCoupleTimer>();
        if (rivalTimer != null)
            rivalTimer.GetRoundScores(out rival1Score, out rival2Score);

        _roundVotes[round, 2] = rival1Score;
        _roundVotes[round, 3] = rival2Score;

        Debug.Log($"Round {_currentRound.Value} — P1: {_roundVotes[round, 0]} | P2: {_roundVotes[round, 1]} | R1: {rival1Score} | R2: {rival2Score}");

        Debug.Log($"[SnapshotRoundVotes] Sending to panel — blueR1: {_roundVotes[0, 0]} | blueR2: {_roundVotes[1, 0]} | blueR3: {_roundVotes[2, 0]}");
        Debug.Log($"[SnapshotRoundVotes] Calling UpdateRevealPanelClientRpc");

        UpdateRevealPanelClientRpc(
            _roundVotes[0, 0], _roundVotes[1, 0], _roundVotes[2, 0],
            _roundVotes[0, 1], _roundVotes[1, 1], _roundVotes[2, 1],
            _roundVotes[0, 2], _roundVotes[1, 2], _roundVotes[2, 2],
            _roundVotes[0, 3], _roundVotes[1, 3], _roundVotes[2, 3],
            _currentRound.Value
        );
    }

    [ClientRpc]
    private void UpdateRevealPanelClientRpc(
    int p1R1, int p1R2, int p1R3,
    int p2R1, int p2R2, int p2R3,
    int rival1R1, int rival1R2, int rival1R3,
    int rival2R1, int rival2R2, int rival2R3,
    int currentRound)
    {
        Debug.Log($"[UpdateRevealPanelClientRpc] fired | IsHost: {IsHost}");

        // Find all panels including inactive ones and update them all
        RevealPanelUI[] panels = FindObjectsByType<RevealPanelUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Debug.Log($"[UpdateRevealPanelClientRpc] Panels found: {panels.Length}");

        foreach (RevealPanelUI panel in panels)
        {
            panel.UpdateScores(
                p1R1, p1R2, p1R3,
                p2R1, p2R2, p2R3,
                rival1R1, rival1R2, rival1R3,
                rival2R1, rival2R2, rival2R3,
                currentRound
            );
        }
    }

    // Sends lock-in rumor to the other player's feed
    [ClientRpc]
    private void NotifyLockInClientRpc(int lockedInPlayerNumber)
    {
        int recipientPlayerNumber = lockedInPlayerNumber == 1 ? 2 : 1;

        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            if (uiManager.GetPlayerNumber() != recipientPlayerNumber) continue;

            RumorFeed feed = uiManager.GetComponentInChildren<RumorFeed>(true);
            if (feed == null) continue;

            feed.AddDirectRumor($"Player {lockedInPlayerNumber} has locked in their ballots");
        }
    }

    private IEnumerator RevealSequence()
    {
        _revealActive.Value = true;
        Debug.Log("Reveal phase started");

        SnapshotRoundVotes();

        yield return new WaitForSecondsRealtime(_revealDuration);

        _revealActive.Value = false;
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;

        if (_currentRound.Value < _totalRounds)
            _currentRound.Value++;

        ResetBallotsClientRpc();

        // Start the rival timer for the next round
        StartRivalTimerClientRpc();

        Debug.Log($"Reveal phase ended — now on round {_currentRound.Value}");
    }

    [ClientRpc]
    private void StartRivalTimerClientRpc()
    {
        RivalCoupleTimer rivalTimer = FindFirstObjectByType<RivalCoupleTimer>();
        if (rivalTimer != null)
            rivalTimer.StartNextRound();
    }

    [ClientRpc]
    private void ResetBallotsClientRpc()
    {
        BallotCollector[] collectors = FindObjectsByType<BallotCollector>(FindObjectsSortMode.None);
        foreach (BallotCollector collector in collectors)
            collector.ResetForNewRound();
    }

    private void OnRevealStateChanged(bool previous, bool current)
    {
        Debug.Log($"OnRevealStateChanged — active: {current} | IsOwner: {IsOwner}");
        // Pause or resume local game based on reveal state
        Time.timeScale = current ? 0f : 1f;

        // Show or hide the reveal panel on the local player's UI
        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            uiManager.SetRevealPanel(current);
        }
    }

    public bool IsRevealActive => _revealActive.Value;
    public int CurrentRound => _currentRound.Value;

    public bool IsPlayerLockedIn(int playerNumber) =>
        playerNumber == 1 ? _player1LockedIn.Value : _player2LockedIn.Value;
}
