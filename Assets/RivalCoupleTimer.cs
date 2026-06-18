using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class RivalCoupleTimer : NetworkBehaviour
{
    public Slider rivalProgressBar;
    public float roundDuration = 120f;
    public int totalRounds = 3;

    [SerializeField] private int ballotsPerRound = 50;

    private float ballotsPerSecond;
    private int maxBallots;
    private bool _tutorialShown = false;

    // Server-authoritative state
    private NetworkVariable<float> _syncedElapsed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _currentRound = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _collectionPhaseActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _roundEndedByTimer = false;

    public override void OnNetworkSpawn()
    {
        maxBallots = ballotsPerRound * totalRounds;
        ballotsPerSecond = (float)ballotsPerRound / roundDuration;

        rivalProgressBar.minValue = 0;
        rivalProgressBar.maxValue = maxBallots;
        rivalProgressBar.value = 0;

        if (IsServer)
        {
            _syncedElapsed.Value = 0f;
            _currentRound.Value = 0;
            _collectionPhaseActive.Value = true;
            _roundEndedByTimer = false;
            Debug.Log("[RivalCoupleTimer] Server initialised.");
        }

        // No OnValueChanged subscriptions needed anymore
        Debug.Log($"[RivalCoupleTimer] OnNetworkSpawn — IsServer:{IsServer} IsClient:{IsClient}");
    }

    public override void OnNetworkDespawn()
    {
       
    }

    private void OnElapsedTimeChanged(float previous, float current)
    {
        Debug.Log($"[RivalCoupleTimer] OnElapsedTimeChanged fired — IsServer:{IsServer} " +
                  $"prev:{previous:F2} curr:{current:F2}");
        UpdateProgressBar(current);
    }

    private void OnRoundChanged(int previous, int current)
    {
        Debug.Log($"[RivalCoupleTimer] OnRoundChanged fired — IsServer:{IsServer} " +
                  $"prev:{previous} curr:{current}");
        UpdateProgressBar(_syncedElapsed.Value);
    }

    private void UpdateProgressBar(float elapsed)
    {
        float roundBallots = Mathf.Min(elapsed * ballotsPerSecond, ballotsPerRound);
        float newValue = (_currentRound.Value * ballotsPerRound) + roundBallots;

        Debug.Log($"[RivalCoupleTimer] UpdateProgressBar — IsServer:{IsServer} " +
                  $"elapsed:{elapsed:F2} roundBallots:{roundBallots:F2} barValue:{newValue:F2}");

        rivalProgressBar.value = newValue;
    }
    void Update()
    {
        if (!_tutorialShown && _currentRound.Value == 0 && _syncedElapsed.Value >= roundDuration * 0.5f)
        {
            _tutorialShown = true;
            TutorialManager.Instance?.ShowPrompt(TutorialManager.TutorialType.RivalGoal);
        }

        if (IsServer)
        {
            if (!_collectionPhaseActive.Value || _currentRound.Value >= totalRounds) return;

            _syncedElapsed.Value += Time.deltaTime;

            if (_syncedElapsed.Value >= roundDuration && !_roundEndedByTimer)
            {
                _roundEndedByTimer = true;
                _syncedElapsed.Value = roundDuration;
                _collectionPhaseActive.Value = false;

                Debug.Log($"[RivalCoupleTimer] Round {_currentRound.Value + 1} ended!");

                if (RoundManager.Instance != null)
                    RoundManager.Instance.ForceEndRoundServerRpc();
            }
        }

        UpdateProgressBar(_syncedElapsed.Value);
    }

    public void StartNextRound()
    {
        if (!IsServer) return;

        _currentRound.Value++;

        if (_currentRound.Value < totalRounds)
        {
            _syncedElapsed.Value = 0f;       
            _collectionPhaseActive.Value = true;
            _roundEndedByTimer = false;
            Debug.Log($"Round {_currentRound.Value + 1} started!");
        }
        else
        {
            Debug.Log("All rounds finished — Rival Couple bar complete!");
        }
    }

    // Called on server only — scores are calculated from authoritative state
    public void GetRoundScores(out int rival1Score, out int rival2Score)
    {
        if (!IsServer)
        {
            Debug.LogWarning("GetRoundScores called on a client — returning zeros.");
            rival1Score = 0;
            rival2Score = 0;
            return;
        }

        float roundBallots = Mathf.Min(_syncedElapsed.Value * ballotsPerSecond, ballotsPerRound);
        int total = Mathf.RoundToInt(roundBallots);
        rival1Score = total / 2;
        rival2Score = total - rival1Score;
    }

    public void RevealPhase()
    {
        int ballots = Mathf.RoundToInt(rivalProgressBar.value);
        Debug.Log($"Reveal Phase: Rival Couple currently has {ballots} ballots!");
    }

    public int GetCurrentRound() => _currentRound.Value;
}