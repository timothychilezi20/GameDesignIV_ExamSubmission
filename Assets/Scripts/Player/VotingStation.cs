using UnityEngine;
using Unity.Netcode;

public class VotingStation : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        TutorialManager.Instance?.ShowPrompt(TutorialManager.TutorialType.VotingStation);

        BallotCollector ballot = other.GetComponent<BallotCollector>();
        if (ballot == null)
            ballot = other.GetComponentInParent<BallotCollector>();
        if (ballot == null) return;

        ballot.SetCurrentStation(this);
        Debug.Log($"Player entered voting station — ballots: {ballot.GetBallotCount()}");

        // Show prompt only if player has ballots to dump
        if (ballot.GetBallotCount() > 0)
            ProximityPromptUI.Instance?.ShowPrompt("Press E to dump ballots", null);
    }

    private void OnTriggerExit(Collider other)
    {
        BallotCollector ballot = other.GetComponent<BallotCollector>();
        if (ballot == null)
            ballot = other.GetComponentInParent<BallotCollector>();
        if (ballot == null) return;

        ballot.ClearCurrentStation();
        Debug.Log("Player left voting station");

        ProximityPromptUI.Instance?.HidePrompt(null);
    }


}