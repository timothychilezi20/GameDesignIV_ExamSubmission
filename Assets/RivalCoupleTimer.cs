using UnityEngine;
using Unity.Netcode;

public class RivalCoupleTimer : NetworkBehaviour
{
    public float roundDuration = 120f;
    public int totalRounds = 3;
    public int ballotsPerRound = 50;

    private float ballotsPerSecond;
    private bool _tutorialShown = false;

    private NetworkVariable<float> _syncedElapsed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _currentRound = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> _collectionPhaseActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool _roundEndedByTimer = false;

    private int[] rival1RoundScores;
    private int[] rival2RoundScores;

    public override void OnNetworkSpawn()
    {
        ballotsPerSecond = (float)ballotsPerRound / roundDuration;
        rival1RoundScores = new int[totalRounds];
        rival2RoundScores = new int[totalRounds];

        if (IsServer)
        {
            _syncedElapsed.Value = 0f;
            _currentRound.Value = 0;
            _collectionPhaseActive.Value = true;
            _roundEndedByTimer = false;
        }
    }

    void Update()
    {
        if (!_tutorialShown && _currentRound.Value == 0 && _syncedElapsed.Value >= roundDuration * 0.5f)
        {
            _tutorialShown = true;
            TutorialManager.Instance?.ShowPrompt(TutorialManager.TutorialType.RivalGoal);
        }

        if (IsServer && _collectionPhaseActive.Value && _currentRound.Value < totalRounds)
        {
            _syncedElapsed.Value += Time.deltaTime;

            if (_syncedElapsed.Value >= roundDuration && !_roundEndedByTimer)
            {
                _roundEndedByTimer = true;
                _syncedElapsed.Value = roundDuration;
                _collectionPhaseActive.Value = false;

                StoreRoundScores();

                RoundManager.Instance?.ForceEndRoundServerRpc();
            }
        }
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
        }
    }

    private void StoreRoundScores()
    {
        SnapshotCurrentRound();
    }

    public void SnapshotCurrentRound()
    {
        float roundBallots = Mathf.Min(_syncedElapsed.Value * ballotsPerSecond, ballotsPerRound);
        int total = Mathf.RoundToInt(roundBallots);
        int rival1Score = total / 2;
        int rival2Score = total - rival1Score;

        rival1RoundScores[_currentRound.Value] = rival1Score;
        rival2RoundScores[_currentRound.Value] = rival2Score;

        Debug.Log($"[RivalCoupleTimer] Snapshot round {_currentRound.Value + 1}: R1={rival1Score}, R2={rival2Score}");
    }

    public float GetBarValue()
    {
        float roundBallots = Mathf.Min(_syncedElapsed.Value * ballotsPerSecond, ballotsPerRound);

        return (_currentRound.Value * ballotsPerRound) + roundBallots;
    }


    public void GetRoundScores(out int rival1Score, out int rival2Score)
    {
        float roundBallots = Mathf.Min(_syncedElapsed.Value * ballotsPerSecond, ballotsPerRound);
        int total = Mathf.RoundToInt(roundBallots);
        rival1Score = total / 2;
        rival2Score = total - rival1Score;
    }

    public int GetCurrentRound() => _currentRound.Value;
}
