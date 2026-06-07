using UnityEngine;
using Unity.Netcode;

public class VotingStation : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Try both BallotCollector directly and via NetworkObject
        BallotCollector ballot = other.GetComponent<BallotCollector>();

        // Also check parent in case collider is on a child object
        if (ballot == null)
            ballot = other.GetComponentInParent<BallotCollector>();

        if (ballot == null) return;

        // Don't require IsOwner here — let BallotCollector's own
        // IsOwner checks handle authority. This way both host and
        // client trigger detection works regardless of CC state.
        ballot.SetCurrentStation(this);
        Debug.Log($"Player entered voting station — ballots: {ballot.GetBallotCount()}");
    }

    private void OnTriggerExit(Collider other)
    {
        BallotCollector ballot = other.GetComponent<BallotCollector>();
        if (ballot == null)
            ballot = other.GetComponentInParent<BallotCollector>();
        if (ballot == null) return;

        ballot.ClearCurrentStation();
        Debug.Log("Player left voting station");
    }

    
}