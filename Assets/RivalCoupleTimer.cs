using UnityEngine;
using UnityEngine.UI;

public class RivalCoupleTimer : MonoBehaviour
{
    public Slider rivalProgressBar;
    public float roundDuration = 5f;  
    public int totalRounds = 3;        
    private int currentRound = 0;
    private float elapsedTime;

    public bool collectionPhaseActive = false;

    void Start()
    {
        rivalProgressBar.minValue = 0;
        rivalProgressBar.maxValue = roundDuration * totalRounds; 
        rivalProgressBar.value = 0;
    }

    void Update()
    {
        if (collectionPhaseActive && currentRound < totalRounds)
        {
            elapsedTime += Time.deltaTime;
            rivalProgressBar.value = (currentRound * roundDuration) + elapsedTime;

            if (elapsedTime >= roundDuration)
            {
                // End of round reached
                elapsedTime = roundDuration;
                rivalProgressBar.value = (currentRound * roundDuration) + roundDuration;

                collectionPhaseActive = false; 
                Debug.Log($"Round {currentRound + 1} ended!");
                currentRound++;
            }
        }
    }

    public void StartNextRound()
    {
        if (currentRound < totalRounds)
        {
            elapsedTime = 0;
            collectionPhaseActive = true;
            Debug.Log($"Round {currentRound} started!");
            
        }
        else
        {
            Debug.Log("All rounds finished — Rival Couple timer complete!");
        }
    }
}