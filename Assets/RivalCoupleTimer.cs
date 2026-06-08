using UnityEngine;
using UnityEngine.UI;

public class RivalCoupleTimer : MonoBehaviour
{
    public Slider rivalProgressBar;
    public float roundDuration = 120f;
    public int totalRounds = 3;
    private int currentRound = 0;
    private float elapsedTime;
    public bool collectionPhaseActive = false;

    [SerializeField] private int ballotsPerRound = 50;
    private float ballotsPerSecond;
    private int maxBallots;

    private bool _roundEndedByTimer = false;

    void Start()
    {
        maxBallots = ballotsPerRound * totalRounds;
        ballotsPerSecond = (float)ballotsPerRound / roundDuration;

        rivalProgressBar.minValue = 0;
        rivalProgressBar.maxValue = maxBallots;
        rivalProgressBar.value = 0;

        // Don't call StartNextRound here — initialise directly
        elapsedTime = 0;
        collectionPhaseActive = true;
        _roundEndedByTimer = false;
        Debug.Log("Round 1 started!");
    }

    void Update()
    {
        // Tutorial trigger at 50% of round 1
        if (currentRound == 0 && elapsedTime >= roundDuration * 0.5f)
            TutorialManager.Instance?.ShowPrompt(TutorialManager.TutorialType.RivalGoal);

        if (!collectionPhaseActive || currentRound >= totalRounds) return;

        elapsedTime += Time.deltaTime;

        float roundBallots = Mathf.Min(elapsedTime * ballotsPerSecond, ballotsPerRound);
        rivalProgressBar.value = (currentRound * ballotsPerRound) + roundBallots;

        if (elapsedTime >= roundDuration && !_roundEndedByTimer)
        {
            _roundEndedByTimer = true;
            elapsedTime = roundDuration;
            rivalProgressBar.value = (currentRound * ballotsPerRound) + ballotsPerRound;
            collectionPhaseActive = false;

            Debug.Log($"Round {currentRound + 1} timer ended at {rivalProgressBar.value} ballots!");

            // Force end the round — RoundManager will call StartNextRound via ClientRpc
            if (RoundManager.Instance != null)
                RoundManager.Instance.ForceEndRoundServerRpc();

            // Don't increment currentRound here — StartNextRound handles it
        }
    }

    public void StartNextRound()
    {
        // Only called by RoundManager after a round ends
        currentRound++;

        if (currentRound < totalRounds)
        {
            elapsedTime = 0;
            collectionPhaseActive = true;
            _roundEndedByTimer = false;
            Debug.Log($"Round {currentRound + 1} started!");
        }
        else
        {
            Debug.Log("All rounds finished — Rival Couple bar complete!");
        }
    }

    public void GetRoundScores(out int rival1Score, out int rival2Score)
    {
        float roundBallots = Mathf.Min(elapsedTime * ballotsPerSecond, ballotsPerRound);
        int total = Mathf.RoundToInt(roundBallots);
        rival1Score = total / 2;
        rival2Score = total - rival1Score;
    }

    public void RevealPhase()
    {
        int ballots = Mathf.RoundToInt(rivalProgressBar.value);
        Debug.Log($"Reveal Phase: Rival Couple currently has {ballots} ballots!");
    }

    public int GetCurrentRound() => currentRound;
}