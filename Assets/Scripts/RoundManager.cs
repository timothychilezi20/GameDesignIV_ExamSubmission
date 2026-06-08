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

    [Header("Compatible Cliques")]
    private CliqueGroup.CliqueType[] _compatibleCliques = new CliqueGroup.CliqueType[2];

    public CliqueGroup.CliqueType[] GetCompatibleCliques()
    {
        // Always read from NetworkVariables so clients get synced values
        if (_compatibleClique1.Value >= 0 && _compatibleClique2.Value >= 0)
        {
            return new CliqueGroup.CliqueType[]
            {
            (CliqueGroup.CliqueType)_compatibleClique1.Value,
            (CliqueGroup.CliqueType)_compatibleClique2.Value
            };
        }
        return _compatibleCliques;
    }

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

    // Sync compatible cliques to all clients
    private NetworkVariable<int> _compatibleClique1 = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _compatibleClique2 = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

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

        if (IsServer)
            PickCompatibleCliques();
    }

    // Add to OnNetworkSpawn or start of game
    private void PickCompatibleCliques()
    {
        System.Collections.Generic.List<CliqueGroup.CliqueType> allTypes = new System.Collections.Generic.List<CliqueGroup.CliqueType>
    {
        CliqueGroup.CliqueType.Artists,
        CliqueGroup.CliqueType.Nerds,
        CliqueGroup.CliqueType.Athletes
    };

        int index1 = Random.Range(0, allTypes.Count);
        _compatibleCliques[0] = allTypes[index1];
        allTypes.RemoveAt(index1);

        int index2 = Random.Range(0, allTypes.Count);
        _compatibleCliques[1] = allTypes[index2];

        // Sync to all clients via NetworkVariables
        _compatibleClique1.Value = (int)_compatibleCliques[0];
        _compatibleClique2.Value = (int)_compatibleCliques[1];

        Debug.Log($"[RoundManager] Compatible cliques: {_compatibleCliques[0]} and {_compatibleCliques[1]}");

        NotifyCompatibleCliquesClientRpc(
            (int)_compatibleCliques[0],
            (int)_compatibleCliques[1]
        );
    }

    [ClientRpc]
    private void NotifyCompatibleCliquesClientRpc(int clique1, int clique2)
    {
        string name1 = ((CliqueGroup.CliqueType)clique1).ToString();
        string name2 = ((CliqueGroup.CliqueType)clique2).ToString();
        string message = $"Compatible cliques this round: {name1} and {name2} — matching ballots will be doubled!";

        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            RumorFeed feed = uiManager.GetComponentInChildren<RumorFeed>(true);
            if (feed != null)
                feed.AddDirectRumor(message);
        }
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
            Debug.Log("Both locked in — starting reveal with multiplier");
            StartCoroutine(RevealSequence(true)); // multiplier applies
        }
    }

    private void SnapshotRoundVotes(bool applyMultiplier)
    {
        if (VoteManager.Instance == null) return;

        int round = _currentRound.Value - 1;

        int p1Total = VoteManager.Instance.GetPlayer1Votes();
        int p2Total = VoteManager.Instance.GetPlayer2Votes();
        int p1Compatible = VoteManager.Instance.GetPlayer1CompatibleVotes();
        int p2Compatible = VoteManager.Instance.GetPlayer2CompatibleVotes();

        int p1RoundVotes = p1Total - _player1VotesAtRoundStart;
        int p2RoundVotes = p2Total - _player2VotesAtRoundStart;

        Debug.Log($"[SnapshotRoundVotes] Round: {_currentRound.Value} | applyMultiplier: {applyMultiplier}");
        Debug.Log($"[SnapshotRoundVotes] P1 total: {p1Total} | P1 start: {_player1VotesAtRoundStart} | P1 round votes: {p1RoundVotes} | P1 compatible: {p1Compatible}");
        Debug.Log($"[SnapshotRoundVotes] P2 total: {p2Total} | P2 start: {_player2VotesAtRoundStart} | P2 round votes: {p2RoundVotes} | P2 compatible: {p2Compatible}");

        if (applyMultiplier)
        {
            int p1Incompatible = p1RoundVotes - p1Compatible;
            int p2Incompatible = p2RoundVotes - p2Compatible;

            Debug.Log($"[SnapshotRoundVotes] P1 — compatible: {p1Compatible} x2 = {p1Compatible * 2} | incompatible: {p1Incompatible} | final: {(p1Compatible * 2) + p1Incompatible}");
            Debug.Log($"[SnapshotRoundVotes] P2 — compatible: {p2Compatible} x2 = {p2Compatible * 2} | incompatible: {p2Incompatible} | final: {(p2Compatible * 2) + p2Incompatible}");

            p1RoundVotes = (p1Compatible * 2) + p1Incompatible;
            p2RoundVotes = (p2Compatible * 2) + p2Incompatible;
        }

        _roundVotes[round, 0] = p1RoundVotes;
        _roundVotes[round, 1] = p2RoundVotes;

        // Update start values for next round
        _player1VotesAtRoundStart = p1Total;
        _player2VotesAtRoundStart = p2Total;

        int rival1Score = 0;
        int rival2Score = 0;
        RivalCoupleTimer rivalTimer = FindFirstObjectByType<RivalCoupleTimer>();
        if (rivalTimer != null)
            rivalTimer.GetRoundScores(out rival1Score, out rival2Score);

        _roundVotes[round, 2] = rival1Score;
        _roundVotes[round, 3] = rival2Score;

        Debug.Log($"[SnapshotRoundVotes] Final — P1: {_roundVotes[round, 0]} | P2: {_roundVotes[round, 1]} | R1: {rival1Score} | R2: {rival2Score}");

        UpdateRevealPanelClientRpc(
            _roundVotes[0, 0], _roundVotes[1, 0], _roundVotes[2, 0],
            _roundVotes[0, 1], _roundVotes[1, 1], _roundVotes[2, 1],
            _roundVotes[0, 2], _roundVotes[1, 2], _roundVotes[2, 2],
            _roundVotes[0, 3], _roundVotes[1, 3], _roundVotes[2, 3],
            _currentRound.Value
        );
    }

    private int ApplyCompatibleMultiplier(int baseVotes, int playerNumber)
    {
        if (VoteManager.Instance == null) return baseVotes;

        int compatibleVotes = playerNumber == 1
            ? VoteManager.Instance.GetPlayer1CompatibleVotes()
            : VoteManager.Instance.GetPlayer2CompatibleVotes();

        int incompatibleVotes = baseVotes - compatibleVotes;
        int total = (compatibleVotes * 2) + incompatibleVotes;

        Debug.Log($"[ApplyCompatibleMultiplier] P{playerNumber} — base: {baseVotes} | compatible: {compatibleVotes} | incompatible: {incompatibleVotes} | total after multiplier: {total}");

        return total;
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

    private IEnumerator RevealSequence(bool applyMultiplier = true)
    {
        _revealActive.Value = true;
        AudioManager.Instance?.PlayRevealBuildup();
        SnapshotRoundVotes(applyMultiplier);

        yield return new WaitForSecondsRealtime(_revealDuration);

        _revealActive.Value = false;
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;

        if (_currentRound.Value >= _totalRounds)
        {
            CheckWinCondition();
            yield break;
        }

        if (_currentRound.Value < _totalRounds)
            _currentRound.Value++;

        VoteManager.Instance?.ResetRoundVotes();

        // Update start values for next round delta
        if (VoteManager.Instance != null)
        {
            _player1VotesAtRoundStart = VoteManager.Instance.GetPlayer1Votes();
            _player2VotesAtRoundStart = VoteManager.Instance.GetPlayer2Votes();
            Debug.Log($"[RevealSequence] New round start values — P1: {_player1VotesAtRoundStart} | P2: {_player2VotesAtRoundStart}");
        }

        PickCompatibleCliques(); // only once
        ResetBallotsClientRpc();
        StartRivalTimerClientRpc();

        AudioManager.Instance?.PlayMusic(AudioManager.MusicState.Exploration);

        Debug.Log($"Reveal phase ended — now on round {_currentRound.Value}");
    }

    private void CheckWinCondition()
    {
        // Add up all blue team votes across all rounds
        int blueTotal = 0;
        int redTotal = 0;

        for (int r = 0; r < _totalRounds; r++)
        {
            blueTotal += _roundVotes[r, 0] + _roundVotes[r, 1]; // P1 + P2
            redTotal += _roundVotes[r, 2] + _roundVotes[r, 3];  // R1 + R2
        }

        bool playersWon = blueTotal > redTotal;

        Debug.Log($"[CheckWinCondition] Blue: {blueTotal} | Red: {redTotal} | Players won: {playersWon}");

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.ShowGameOverClientRpc(playersWon);
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

    [ServerRpc]
    public void ForceEndRoundServerRpc()
    {
        Debug.Log("[RoundManager] Round force ended by rival timer — no multiplier");

        // Reset lock in state
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;

        StartCoroutine(RevealSequence(false)); // no multiplier
    }

    public bool IsRevealActive => _revealActive.Value;
    public int CurrentRound => _currentRound.Value;

    public bool IsPlayerLockedIn(int playerNumber) =>
        playerNumber == 1 ? _player1LockedIn.Value : _player2LockedIn.Value;
}
