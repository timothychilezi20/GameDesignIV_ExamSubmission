using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Reveal Settings")]
    [SerializeField] private float _revealDuration = 30f;
    [SerializeField] private int _totalRounds = 3;

    // ─── Compatible Cliques ───────────────────────────────────────

    private CliqueGroup.CliqueType[] _compatibleCliques = new CliqueGroup.CliqueType[2];

    private NetworkVariable<int> _compatibleClique1 = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _compatibleClique2 = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public CliqueGroup.CliqueType[] GetCompatibleCliques()
    {
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

    // ─── Network Variables ────────────────────────────────────────

    private NetworkVariable<bool> _player1LockedIn = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<bool> _player2LockedIn = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<bool> _revealActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _currentRound = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // ─── Score Tracking ───────────────────────────────────────────

    // [round 0-2][0=P1, 1=P2, 2=Rival1, 3=Rival2]
    private int[,] _roundVotes = new int[3, 4];

    private int _player1VotesAtRoundStart = 0;
    private int _player2VotesAtRoundStart = 0;

    // ─── Guard ────────────────────────────────────────────────────

    // Prevents RevealSequence running twice if both players lock in
    // at the same moment the rival timer fires
    private bool _revealRunning = false;

    // ──────────────────────────────────────────────────────────────

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
        _revealActive.OnValueChanged += OnRevealStateChanged;

        if (IsServer)
            StartRound(1);
    }

    public override void OnNetworkDespawn()
    {
        _revealActive.OnValueChanged -= OnRevealStateChanged;
    }

    // ─── Round Start ──────────────────────────────────────────────

    private void StartRound(int roundNumber)
    {
        _currentRound.Value = roundNumber;

        // Randomise which two cliques are on good terms this round.
        // CliqueRelationshipManager syncs this to clients via its own ClientRpc.
        if (CliqueRelationshipManager.Instance != null)
            CliqueRelationshipManager.Instance.RandomiseRelationshipsForRound(roundNumber);

        // Reset per-round breakdown stats used by the reveal log
        if (RoundStats.Instance != null)
            RoundStats.Instance.ResetForNewRound();

        PickCompatibleCliques();

        Debug.Log($"[RoundManager] Round {roundNumber} started");
        NotifyRoundStartClientRpc(roundNumber);
    }

    // ─── Compatible Clique Selection ──────────────────────────────

    private void PickCompatibleCliques()
    {
        System.Collections.Generic.List<CliqueGroup.CliqueType> allTypes =
            new System.Collections.Generic.List<CliqueGroup.CliqueType>
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

    // ─── Lock In ──────────────────────────────────────────────────

    [ServerRpc]
    public void LockInVotesServerRpc(int playerNumber)
    {
        if (_revealRunning) return;

        Debug.Log($"LockInVotesServerRpc — playerNumber: {playerNumber}");

        if (playerNumber == 1) _player1LockedIn.Value = true;
        else if (playerNumber == 2) _player2LockedIn.Value = true;

        Debug.Log($"Lock state — P1: {_player1LockedIn.Value} | P2: {_player2LockedIn.Value}");

        NotifyLockInClientRpc(playerNumber);

        if (_player1LockedIn.Value && _player2LockedIn.Value)
        {
            Debug.Log("Both locked in — starting reveal WITH multiplier");
            StartCoroutine(RevealSequence(true));
        }
    }

    // Called directly on the server by RivalCoupleTimer
    [ServerRpc]
    public void ForceEndRoundServerRpc()
    {
        if (_revealRunning) return;

        Debug.Log("[RoundManager] Round force ended by rival timer — no multiplier");
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;
        StartCoroutine(RevealSequence(false));
    }

    // ─── Reveal Sequence ──────────────────────────────────────────

    private IEnumerator RevealSequence(bool applyMultiplier)
    {
        if (_revealRunning) yield break;
        _revealRunning = true;

        _revealActive.Value = true;
        AudioManager.Instance?.PlayRevealBuildup();

        Debug.Log($"[RoundManager] Reveal started — Round {_currentRound.Value} | Multiplier: {applyMultiplier}");

        // Snapshot scores and push to scoreboard panel
        SnapshotRoundVotes(applyMultiplier);

        // Send the per-clique breakdown to all clients for the reveal log
        SendRevealStatsClientRpc(applyMultiplier);

        yield return new WaitForSecondsRealtime(_revealDuration);

        _revealActive.Value = false;
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;

        if (_currentRound.Value >= _totalRounds)
        {
            CheckWinCondition();
            _revealRunning = false;
            yield break;
        }

        _currentRound.Value++;

        VoteManager.Instance?.ResetRoundVotes();

        if (VoteManager.Instance != null)
        {
            _player1VotesAtRoundStart = VoteManager.Instance.GetPlayer1Votes();
            _player2VotesAtRoundStart = VoteManager.Instance.GetPlayer2Votes();
            Debug.Log($"[RevealSequence] New round start — P1: {_player1VotesAtRoundStart} | P2: {_player2VotesAtRoundStart}");
        }

        StartRound(_currentRound.Value);
        ResetBallotsClientRpc();
        StartRivalTimerClientRpc();

        AudioManager.Instance?.PlayMusic(AudioManager.MusicState.Exploration);

        Debug.Log($"[RoundManager] Reveal ended — now on round {_currentRound.Value}");
        _revealRunning = false;
    }

    // ─── Score Snapshot ───────────────────────────────────────────

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

        Debug.Log($"[SnapshotRoundVotes] Round: {_currentRound.Value} | Multiplier: {applyMultiplier}");
        Debug.Log($"[SnapshotRoundVotes] P1 — total: {p1Total} | start: {_player1VotesAtRoundStart} | round: {p1RoundVotes} | compatible: {p1Compatible}");
        Debug.Log($"[SnapshotRoundVotes] P2 — total: {p2Total} | start: {_player2VotesAtRoundStart} | round: {p2RoundVotes} | compatible: {p2Compatible}");

        if (applyMultiplier)
        {
            int p1Incompatible = p1RoundVotes - p1Compatible;
            int p2Incompatible = p2RoundVotes - p2Compatible;

            Debug.Log($"[SnapshotRoundVotes] P1 — compatible: {p1Compatible} x2={p1Compatible * 2} | incompatible: {p1Incompatible} | final: {(p1Compatible * 2) + p1Incompatible}");
            Debug.Log($"[SnapshotRoundVotes] P2 — compatible: {p2Compatible} x2={p2Compatible * 2} | incompatible: {p2Incompatible} | final: {(p2Compatible * 2) + p2Incompatible}");

            p1RoundVotes = (p1Compatible * 2) + p1Incompatible;
            p2RoundVotes = (p2Compatible * 2) + p2Incompatible;
        }

        _roundVotes[round, 0] = p1RoundVotes;
        _roundVotes[round, 1] = p2RoundVotes;

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
            _currentRound.Value,
            applyMultiplier,
            _compatibleClique1.Value,
            _compatibleClique2.Value
        );
    }

    // ─── Win Condition ────────────────────────────────────────────

    private void CheckWinCondition()
    {
        int blueTotal = 0;
        int redTotal = 0;

        for (int r = 0; r < _totalRounds; r++)
        {
            blueTotal += _roundVotes[r, 0] + _roundVotes[r, 1];
            redTotal += _roundVotes[r, 2] + _roundVotes[r, 3];
        }

        bool playersWon = blueTotal > redTotal;
        Debug.Log($"[CheckWinCondition] Blue: {blueTotal} | Red: {redTotal} | Players won: {playersWon}");

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.ShowGameOverClientRpc(playersWon);
    }

    // ─── Client RPCs ──────────────────────────────────────────────

    [ClientRpc]
    private void NotifyRoundStartClientRpc(int roundNumber)
    {
        Debug.Log($"[RoundManager] Round {roundNumber} begun");
        Time.timeScale = 1f;
    }

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
            if (feed != null)
                feed.AddDirectRumor($"Player {lockedInPlayerNumber} has locked in their ballots");
        }
    }


    // Passes multiplier state and compatible clique indices so
    // RevealPanelUI can highlight which cliques earned the bonus
    [ClientRpc]
    private void UpdateRevealPanelClientRpc(
        int p1R1, int p1R2, int p1R3,
        int p2R1, int p2R2, int p2R3,
        int r1R1, int r1R2, int r1R3,
        int r2R1, int r2R2, int r2R3,
        int currentRound,
        bool multiplierActive,
        int compatibleClique1,
        int compatibleClique2)
    {
        Debug.Log($"[UpdateRevealPanelClientRpc] Round: {currentRound} | Multiplier: {multiplierActive}");

        RevealPanelUI[] panels = FindObjectsByType<RevealPanelUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (RevealPanelUI panel in panels)
        {
            panel.UpdateScores(
                p1R1, p1R2, p1R3,
                p2R1, p2R2, p2R3,
                r1R1, r1R2, r1R3,
                r2R1, r2R2, r2R3,
                currentRound,
                multiplierActive,
                compatibleClique1,
                compatibleClique2
            );
        }
    }

    // Sends the detailed per-clique breakdown for the reveal log on each client.
    // Uses RoundStats so the breakdown reflects actual clique-by-clique collection.
    [ClientRpc]
    private void SendRevealStatsClientRpc(bool multiplierActive)
    {
        if (RoundStats.Instance == null) return;

        int p1Artists = RoundStats.Instance.GetPlayerArtists(1);
        int p1Nerds = RoundStats.Instance.GetPlayerNerds(1);
        int p1Athletes = RoundStats.Instance.GetPlayerAthletes(1);

        int p2Artists = RoundStats.Instance.GetPlayerArtists(2);
        int p2Nerds = RoundStats.Instance.GetPlayerNerds(2);
        int p2Athletes = RoundStats.Instance.GetPlayerAthletes(2);

        int p1Score = multiplierActive
            ? RoundStats.Instance.GetPlayerScoreWithMultiplier(1)
            : RoundStats.Instance.GetPlayerTotal(1);

        int p2Score = multiplierActive
            ? RoundStats.Instance.GetPlayerScoreWithMultiplier(2)
            : RoundStats.Instance.GetPlayerTotal(2);

        int roundTotal = RoundStats.Instance.GetRoundTotal();

        string goodTerms = "";
        if (CliqueRelationshipManager.Instance != null && multiplierActive)
        {
            string c1 = CliqueRelationshipManager.Instance.GetGoodTermsClique1().ToString();
            string c2 = CliqueRelationshipManager.Instance.GetGoodTermsClique2().ToString();
            goodTerms = $" | Good terms: {c1} + {c2} (x2)";
        }

        Debug.Log($"─── REVEAL STATS — Round {_currentRound.Value}{goodTerms} ───");
        Debug.Log($"Player 1 — Artists: {p1Artists} | Nerds: {p1Nerds} | Athletes: {p1Athletes} | Score: {p1Score}");
        Debug.Log($"Player 2 — Artists: {p2Artists} | Nerds: {p2Nerds} | Athletes: {p2Athletes} | Score: {p2Score}");
        Debug.Log($"Total ballots this round: {roundTotal} | Multiplier applied: {multiplierActive}");
    }

    [ClientRpc]
    private void ResetBallotsClientRpc()
    {
        BallotCollector[] collectors = FindObjectsByType<BallotCollector>(FindObjectsSortMode.None);
        foreach (BallotCollector collector in collectors)
            collector.ResetForNewRound();

        CliqueGroup[] groups = FindObjectsByType<CliqueGroup>(FindObjectsSortMode.None);
        foreach (CliqueGroup group in groups)
            group.ResetForNewRound();
    }

    [ClientRpc]
    private void StartRivalTimerClientRpc()
    {
        // Only the server should drive the timer state
        if (!IsServer) return;

        RivalCoupleTimer rivalTimer = FindFirstObjectByType<RivalCoupleTimer>();
        if (rivalTimer != null)
            rivalTimer.StartNextRound();
    }

    // ─── State Changes ────────────────────────────────────────────

    private void OnRevealStateChanged(bool previous, bool current)
    {
        Debug.Log($"[RoundManager] OnRevealStateChanged — active: {current}");
        Time.timeScale = current ? 0f : 1f;

        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            uiManager.SetRevealPanel(current);
        }
    }

    // ─── Public API ───────────────────────────────────────────────

    public bool IsRevealActive => _revealActive.Value;
    public int CurrentRound => _currentRound.Value;

    public bool IsPlayerLockedIn(int playerNumber) =>
        playerNumber == 1 ? _player1LockedIn.Value : _player2LockedIn.Value;
}