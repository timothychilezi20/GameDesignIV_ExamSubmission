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

    // Called by Gossipier.LogPlayerSpotted on the server.
    // Routes the sighting to the OTHER player's rumor feed —
    // spottings of Player 1 go to Player 2's feed and vice versa.
    // RequireOwnership = false so Gossipier (not a NetworkObject owner)
    // can call this from any client context.
    [ServerRpc]
    public void ReportSpottingServerRpc(int spottedPlayerNumber, string areaName, bool isInterior)
    {
        // Send to the player who is NOT the one spotted
        int recipientPlayerNumber = spottedPlayerNumber == 1 ? 2 : 1;
        SendToRecipientClientRpc(spottedPlayerNumber, areaName, isInterior, recipientPlayerNumber);
    }

    // Runs on ALL clients but only the matching player activates their feed
    [ClientRpc]
    private void SendToRecipientClientRpc(int spottedPlayerNumber, string areaName, bool isInterior, int recipientPlayerNumber)
    {
        // Find all PlayerUIManagers in the scene — one per spawned player
        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);

        foreach (PlayerUIManager uiManager in allPlayers)
        {
            // Only the local owner whose player number matches the recipient
            if (!uiManager.IsOwner) continue;
            if (uiManager.GetPlayerNumber() != recipientPlayerNumber) continue;

            // Found the correct local player — get their RumorFeed and add the entry
            RumorFeed feed = uiManager.GetComponentInChildren<RumorFeed>(true);
            if (feed == null) continue;

            string playerLabel = $"Player {spottedPlayerNumber}";
            feed.AddRumor(playerLabel, areaName, isInterior);
        }
    }
}