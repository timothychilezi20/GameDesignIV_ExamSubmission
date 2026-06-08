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

    void Start()
    {
        maxBallots = ballotsPerRound * totalRounds; 
        ballotsPerSecond = ballotsPerRound / roundDuration; 

        rivalProgressBar.minValue = 0;
        rivalProgressBar.maxValue = maxBallots; 
        rivalProgressBar.value = 0;

        StartNextRound(); //Only thing added
    }

    void Update()
    {
        if (currentRound == 0 && elapsedTime >= roundDuration * 0.5f)
            TutorialManager.Instance?.ShowPrompt(TutorialManager.TutorialType.RivalGoal); //Added for tutorial

        if (collectionPhaseActive && currentRound < totalRounds)
        {
            elapsedTime += Time.deltaTime;
            float roundBallots = Mathf.Min(elapsedTime * ballotsPerSecond, ballotsPerRound);

            
            rivalProgressBar.value = (currentRound * ballotsPerRound) + roundBallots;

            if (elapsedTime >= roundDuration)
            {
                elapsedTime = roundDuration;
                rivalProgressBar.value = (currentRound * ballotsPerRound) + ballotsPerRound;
                collectionPhaseActive = false;
                Debug.Log($"Round {currentRound + 1} ended at {rivalProgressBar.value} ballots!");
                currentRound++;

                // Force end the round without multiplier
                if (RoundManager.Instance != null)
                    RoundManager.Instance.ForceEndRoundServerRpc();
            }
        }
    }

    public void StartNextRound()
    {
        if (currentRound < totalRounds)
        {
            elapsedTime = 0;
            collectionPhaseActive = true;
            Debug.Log($"Round {currentRound + 1} started!");
        }
        else
        {
            Debug.Log("All rounds finished — Rival Couple bar complete!");
        }
    }

    
    public void RevealPhase()
    {
        int ballots = Mathf.RoundToInt(rivalProgressBar.value);
        Debug.Log($"Reveal Phase: Rival Couple currently has {ballots} ballots!");
    }

    public void GetRoundScores(out int rival1Score, out int rival2Score)
    {
        // Calculate how many ballots were collected this round
        float roundBallots = Mathf.Min(elapsedTime * ballotsPerSecond, ballotsPerRound);

        // Split evenly between the two rivals
        int total = Mathf.RoundToInt(roundBallots);
        rival1Score = total / 2;
        rival2Score = total - rival1Score; // handles odd numbers cleanly
    }

    public int GetCurrentRound() => currentRound;
}
