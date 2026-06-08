using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    private bool _revealFromLockIn = false;

    [Header("Round Settings")]
    [SerializeField] private int _totalRounds = 3;
    [SerializeField] private float _roundDuration = 90f;
    [SerializeField] private float _revealDuration = 30f;

    private NetworkVariable<int> _currentRound = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

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

    private NetworkVariable<bool> _gameOver = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Added: flag to prevent RevealSequence running twice simultaneously
    // Both the round timer and lock-in can trigger it — this blocks double entry
    private bool _revealRunning = false;

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
        _currentRound.OnValueChanged += OnRoundChanged;

        if (IsServer)
            StartCoroutine(RoundTimerRoutine());
    }

    public override void OnNetworkDespawn()
    {
        _revealActive.OnValueChanged -= OnRevealStateChanged;
        _currentRound.OnValueChanged -= OnRoundChanged;
    }

    // ─── Round Timer ──────────────────────────────────────────────

    private IEnumerator RoundTimerRoutine()
    {
        for (int round = 1; round <= _totalRounds; round++)
        {
            _currentRound.Value = round;

            // Randomise clique relationships at the start of each round
            if (CliqueRelationshipManager.Instance != null)
                CliqueRelationshipManager.Instance.RandomiseRelationshipsForRound(round);

            // Reset per-round stats
            if (RoundStats.Instance != null)
                RoundStats.Instance.ResetForNewRound();

            Debug.Log($"Round {round} started — {_roundDuration}s timer running");
            NotifyRoundStartClientRpc(round);

            float elapsed = 0f;
            while (elapsed < _roundDuration)
            {
                if (_revealRunning)
                {
                    yield return new WaitUntil(() => !_revealRunning);
                    break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_revealRunning && !_gameOver.Value)
            {
                Debug.Log($"Round {round} timer expired — triggering reveal (no multiplier)");
                _revealFromLockIn = false;
                yield return StartCoroutine(RevealSequence());
            }
            else if (_revealRunning)
            {
                yield return new WaitUntil(() => !_revealRunning);
            }

            if (_gameOver.Value) break;
        }
    }


    // ─── Lock In ──────────────────────────────────────────────────

   // [ServerRpc]
    //public void LockInVotesServerRpc(int playerNumber)
    //{
    //    // Ignore if reveal already running or game is over
    //    if (_revealRunning || _gameOver.Value) return;

    //    Debug.Log($"LockInVotesServerRpc — playerNumber: {playerNumber}");

    //    if (playerNumber == 1) _player1LockedIn.Value = true;
    //    else if (playerNumber == 2) _player2LockedIn.Value = true;

    //    Debug.Log($"Lock state — P1: {_player1LockedIn.Value} | P2: {_player2LockedIn.Value}");

    //    NotifyLockInClientRpc(playerNumber);

    //    // Only start reveal when BOTH players have locked in
    //    if (_player1LockedIn.Value && _player2LockedIn.Value)
    //    {
    //        Debug.Log("Both players locked in — starting reveal");
    //        StartCoroutine(RevealSequence());
    //    }
    //}



    public void HandleLockIn(int playerNumber)
    {
        if (!IsServer) return;
        if (_revealRunning || _gameOver.Value) return;

        Debug.Log($"HandleLockIn — playerNumber: {playerNumber}");

        if (playerNumber == 1) _player1LockedIn.Value = true;
        else if (playerNumber == 2) _player2LockedIn.Value = true;

        Debug.Log($"Lock state — P1: {_player1LockedIn.Value} | P2: {_player2LockedIn.Value}");

        NotifyLockInClientRpc(playerNumber);

        if (_player1LockedIn.Value && _player2LockedIn.Value)
        {
            Debug.Log("Both locked in — starting reveal WITH multiplier");
            _revealFromLockIn = true;
            StartCoroutine(RevealSequence());
        }
    }

    // ─── Reveal Sequence ──────────────────────────────────────────

    private IEnumerator RevealSequence()
    {
        if (_revealRunning) yield break;
        _revealRunning = true;

        _revealActive.Value = true;
        Debug.Log($"Reveal phase started — Round {_currentRound.Value} | Multiplier active: {_revealFromLockIn}");

        // Send reveal stats to all clients
        if (RoundStats.Instance != null)
            SendRevealStatsClientRpc(_revealFromLockIn);

        yield return new WaitForSecondsRealtime(_revealDuration);

        _revealActive.Value = false;
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;
        _revealFromLockIn = false;

        Debug.Log($"Reveal phase ended — Round {_currentRound.Value} complete");

        if (_currentRound.Value >= _totalRounds)
        {
            _gameOver.Value = true;
            Debug.Log("All rounds complete — game over");
            GameOverClientRpc();
        }
        else
        {
            ResetPlayersForNewRoundClientRpc(_currentRound.Value + 1);
        }

        _revealRunning = false;
    }

    // ─── Client RPCs ──────────────────────────────────────────────

    [ClientRpc]
    private void NotifyRoundStartClientRpc(int roundNumber)
    {
        Debug.Log($"Round {roundNumber} begun");
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

    [ClientRpc]
    private void ResetPlayersForNewRoundClientRpc(int newRoundNumber)
    {
        Debug.Log($"Resetting for Round {newRoundNumber}");
        Time.timeScale = 1f;

        BallotCollector[] collectors = FindObjectsByType<BallotCollector>(FindObjectsSortMode.None);
        foreach (BallotCollector collector in collectors)
        {
            if (!collector.IsOwner) continue;
            collector.ResetForNewRound();
        }

        CliqueGroup[] groups = FindObjectsByType<CliqueGroup>(FindObjectsSortMode.None);
        foreach (CliqueGroup group in groups)
            group.ResetForNewRound();
    }

    [ClientRpc]
    private void GameOverClientRpc()
    {
        Debug.Log("Game over — all rounds complete");
        Time.timeScale = 1f;
    }

    // ─── State Changes ────────────────────────────────────────────

    private void OnRevealStateChanged(bool previous, bool current)
    {
        Debug.Log($"OnRevealStateChanged — active: {current}");
        Time.timeScale = current ? 0f : 1f;

        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            uiManager.SetRevealPanel(current);
        }
    }

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

        Debug.Log($"─── REVEAL STATS — Round {RoundManager.Instance.CurrentRound}{goodTerms} ───");
        Debug.Log($"Player 1 — Artists: {p1Artists} | Nerds: {p1Nerds} | Athletes: {p1Athletes} | Score: {p1Score}");
        Debug.Log($"Player 2 — Artists: {p2Artists} | Nerds: {p2Nerds} | Athletes: {p2Athletes} | Score: {p2Score}");
        Debug.Log($"Total ballots this round: {roundTotal} | Multiplier applied: {multiplierActive}");
    }


    private void OnRoundChanged(int previous, int current)
    {
        Debug.Log($"Round changed — {previous} → {current}");
    }

    public bool IsRevealActive => _revealActive.Value;
    public int CurrentRound => _currentRound.Value;
}