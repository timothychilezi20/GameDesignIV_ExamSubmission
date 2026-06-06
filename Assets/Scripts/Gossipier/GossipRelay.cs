using UnityEngine;
using Unity.Netcode;

// Lightweight NetworkBehaviour that relays gossipier sighting reports
// to the server. Gossipier is a plain MonoBehaviour so it can't call
// ServerRpcs directly — this script acts as the networked bridge.
// Attach to the Player prefab.
public class GossipRelay : NetworkBehaviour
{
    // Only the local owner's relay should forward reports
    // so we find the owned instance before calling
    public void SendSpottingReport(int spottedPlayerNumber, string areaName, bool isInterior)
    {
        if (!IsOwner) return;
        ReportSpottingServerRpc(spottedPlayerNumber, areaName, isInterior);
    }

    [ServerRpc]
    private void ReportSpottingServerRpc(int spottedPlayerNumber, string areaName, bool isInterior)
    {
        RumorManager.Instance.SendRumorClientRpc(spottedPlayerNumber, areaName, isInterior);
    }
}