using UnityEngine;
using Unity.Netcode;

// Singleton NetworkBehaviour that receives gossipier sightings
// from the server and routes them to the correct player's RumorFeed.
// Attach to a persistent NetworkObject in the scene alongside VoteManager.
public class RumorManager : NetworkBehaviour
{
    public static RumorManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportSpottingServerRpc(int spottedPlayerNumber, string areaName, bool isInterior)
    {
        SendRumorClientRpc(spottedPlayerNumber, areaName, isInterior);
    }

    // Called by GossipRelay on the server — routes to recipient client
    [ClientRpc]
    public void SendRumorClientRpc(int spottedPlayerNumber, string areaName, bool isInterior)
    {
        int recipientPlayerNumber = spottedPlayerNumber == 1 ? 2 : 1;

        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        Debug.Log($"SendRumorClientRpc — spotted: {spottedPlayerNumber} | recipient: {recipientPlayerNumber} | players found: {allPlayers.Length}");

        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            if (uiManager.GetPlayerNumber() != recipientPlayerNumber) continue;

            RumorFeed feed = uiManager.GetComponentInChildren<RumorFeed>(true);
            Debug.Log($"Feed found: {feed != null}");
            if (feed == null) continue;

            feed.AddRumor($"Player {spottedPlayerNumber}", areaName, isInterior);
        }
    }

    [ClientRpc]
    public void SendDirectRumorClientRpc(string message, int spottedPlayerNumber)
    {
        // Rumor goes to the OTHER player — not the one spotted
        int recipientPlayerNumber = spottedPlayerNumber == 1 ? 2 : 1;

        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            if (uiManager.GetPlayerNumber() != recipientPlayerNumber) continue;

            RumorFeed feed = uiManager.GetComponentInChildren<RumorFeed>(true);
            if (feed == null) continue;

            feed.AddDirectRumor(message);
        }
    }
}