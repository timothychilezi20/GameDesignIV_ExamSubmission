using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

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
            // Set current round
            _currentRound.Value = round;
            Debug.Log($"Round {round} started — {_roundDuration}s timer running");
            NotifyRoundStartClientRpc(round);

            // Wait for round duration using real time
            float elapsed = 0f;
            while (elapsed < _roundDuration)
            {
                // If reveal already started via lock-in, stop counting
                if (_revealRunning)
                {
                    // Wait for reveal to fully finish before continuing
                    yield return new WaitUntil(() => !_revealRunning);
                    break; // round is over, move to next
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Only trigger reveal from timer if lock-in didn't already do it
            if (!_revealRunning && !_gameOver.Value)
            {
                Debug.Log($"Round {round} timer expired — triggering reveal");
                yield return StartCoroutine(RevealSequence());
            }
            else if (_revealRunning)
            {
                // Already handled by lock-in reveal — just wait
                yield return new WaitUntil(() => !_revealRunning);
            }

            if (_gameOver.Value) break;
        }
    }

    // ─── Lock In ──────────────────────────────────────────────────

    [ServerRpc]
    public void LockInVotesServerRpc(int playerNumber)
    {
        // Ignore if reveal already running or game is over
        if (_revealRunning || _gameOver.Value) return;

        Debug.Log($"LockInVotesServerRpc — playerNumber: {playerNumber}");

        if (playerNumber == 1) _player1LockedIn.Value = true;
        else if (playerNumber == 2) _player2LockedIn.Value = true;

        Debug.Log($"Lock state — P1: {_player1LockedIn.Value} | P2: {_player2LockedIn.Value}");

        NotifyLockInClientRpc(playerNumber);

        // Only start reveal when BOTH players have locked in
        if (_player1LockedIn.Value && _player2LockedIn.Value)
        {
            Debug.Log("Both players locked in — starting reveal");
            StartCoroutine(RevealSequence());
        }
    }

    // ─── Reveal Sequence ──────────────────────────────────────────

    private IEnumerator RevealSequence()
    {
        // Guard against double entry
        if (_revealRunning) yield break;
        _revealRunning = true;

        _revealActive.Value = true;
        Debug.Log($"Reveal phase started — Round {_currentRound.Value}");

        yield return new WaitForSecondsRealtime(_revealDuration);

        _revealActive.Value = false;
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;

        Debug.Log($"Reveal phase ended — Round {_currentRound.Value} complete");

        // Check if this was the last round
        if (_currentRound.Value >= _totalRounds)
        {
            _gameOver.Value = true;
            Debug.Log("All rounds complete — game over");
            GameOverClientRpc();
        }
        else
        {
            // Reset players for next round
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

    private void OnRoundChanged(int previous, int current)
    {
        Debug.Log($"Round changed — {previous} → {current}");
    }

    public bool IsRevealActive => _revealActive.Value;
    public int CurrentRound => _currentRound.Value;
}