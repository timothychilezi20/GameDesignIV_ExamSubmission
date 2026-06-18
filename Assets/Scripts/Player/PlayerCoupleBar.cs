using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerCoupleBar : NetworkBehaviour
{
    [SerializeField] private Slider playerProgressBar;
    [SerializeField] private int maxBallots = 20;

    // Server-authoritative total — all clients read this to update their bar
    private NetworkVariable<int> _totalBallots = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private List<BallotCollector> _collectors = new List<BallotCollector>();
    private float _refreshInterval = 1f;
    private float _refreshTimer = 0f;

    public override void OnNetworkSpawn()
    {
        playerProgressBar.minValue = 0;
        playerProgressBar.maxValue = maxBallots;
        playerProgressBar.value = 0;

        // All clients subscribe so bar updates when server changes the value
        _totalBallots.OnValueChanged += OnBallotsChanged;

        // Force immediate bar update in case value already exists
        playerProgressBar.value = _totalBallots.Value;
    }

    public override void OnNetworkDespawn()
    {
        _totalBallots.OnValueChanged -= OnBallotsChanged;
    }

    private void OnBallotsChanged(int previous, int current)
    {
        playerProgressBar.value = current;
    }

    private void Update()
    {
        // Only server calculates and writes the total
        if (!IsServer) return;

        // Re-scan collectors on an interval instead of every frame
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= _refreshInterval)
        {
            _refreshTimer = 0f;
            RefreshCollectors();
        }

        // Tally ballots from all known collectors
        int total = 0;
        foreach (var collector in _collectors)
        {
            if (collector != null)
                total += collector.GetBallotCount();
        }

        // Only write if changed — avoids unnecessary NetworkVariable traffic
        if (total != _totalBallots.Value)
            _totalBallots.Value = total;
    }

    private void RefreshCollectors()
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
    }
}