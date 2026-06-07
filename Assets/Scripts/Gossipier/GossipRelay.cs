using UnityEngine;
using Unity.Netcode;

public class GossipRelay : NetworkBehaviour
{
    public void SendSpottingReport(int spottedPlayerNumber, string areaName, bool isInterior)
    {
        if (!IsOwner) return;
        ReportSpottingServerRpc(spottedPlayerNumber, areaName, isInterior);
    }

    // Added: for pre-built messages like clique and station reports
    public void SendDirectReport(string message, int spottedPlayerNumber)
    {
        if (!IsOwner) return;
        ReportDirectServerRpc(message, spottedPlayerNumber);
    }

    [ServerRpc]
    private void ReportSpottingServerRpc(int spottedPlayerNumber, string areaName, bool isInterior)
    {
        RumorManager.Instance.SendRumorClientRpc(spottedPlayerNumber, areaName, isInterior);
    }

    // Added: routes pre-built message to RumorManager
    [ServerRpc]
    private void ReportDirectServerRpc(string message, int spottedPlayerNumber)
    {
        RumorManager.Instance.SendDirectRumorClientRpc(message, spottedPlayerNumber);
    }
}