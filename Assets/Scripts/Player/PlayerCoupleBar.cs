using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerCoupleBar : MonoBehaviour
{
    public Slider playerProgressBar;
    [SerializeField] private int maxBallots = 20; // total combined max (10 each for now gang)

    private List<BallotCollector> playerCollectors = new List<BallotCollector>();

    void Start()
    {
        playerProgressBar.minValue = 0;
        playerProgressBar.maxValue = maxBallots;
        playerProgressBar.value = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj != null)
            {
                BallotCollector collector = playerObj.GetComponent<BallotCollector>();
                if (collector != null)
                {
                    playerCollectors.Add(collector);
                }
            }
        }
    }

    void Update()
    {
        int totalBallots = 0;
        foreach (var collector in playerCollectors)
        {
            totalBallots += collector.GetBallotCount();
        }

        playerProgressBar.value = totalBallots;
    }
}
