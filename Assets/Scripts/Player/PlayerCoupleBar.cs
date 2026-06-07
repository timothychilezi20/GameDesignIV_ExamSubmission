using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerCoupleBar : MonoBehaviour
{
    [SerializeField] private Slider playerProgressBar;
    [SerializeField] private int maxBallots = 20;

    private List<BallotCollector> _collectors = new List<BallotCollector>();
    private bool _foundCollectors = false;

    private void Start()
    {
        playerProgressBar.minValue = 0;
        playerProgressBar.maxValue = maxBallots;
        playerProgressBar.value = 0;
    }

    private void Update()
    {
        // Keep trying until we find collectors
        if (!_foundCollectors)
        {
            _collectors.Clear();
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var playerObj = client.PlayerObject;
                if (playerObj == null) continue;

                BallotCollector collector = playerObj.GetComponent<BallotCollector>();
                if (collector != null)
                    _collectors.Add(collector);
            }

            if (_collectors.Count > 0)
                _foundCollectors = true;
        }

        int totalBallots = 0;
        foreach (var collector in _collectors)
        {
            if (collector != null)
                totalBallots += collector.GetBallotCount();
        }

        playerProgressBar.value = totalBallots;
    }
}