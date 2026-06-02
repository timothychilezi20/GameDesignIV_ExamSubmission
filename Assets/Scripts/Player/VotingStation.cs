using UnityEngine;
using Unity.Netcode;

public class VotingStation : MonoBehaviour
{
    private BallotCollector _playerInsideStation = null;

    private void OnTriggerEnter(Collider other)
    {
        BallotCollector ballot = other.GetComponent<BallotCollector>();
        if (ballot == null || !ballot.IsOwner) return;

        _playerInsideStation = ballot;
        Debug.Log("Entered voting station — press E to dump ballots");
    }

    private void OnTriggerExit(Collider other)
    {
        BallotCollector ballot = other.GetComponent<BallotCollector>();
        if (ballot == null || ballot != _playerInsideStation) return;

        _playerInsideStation = null;
        Debug.Log("Left voting station");
    }

    public void TryDumpBallots(BallotCollector ballotCollector)
    {
        if (_playerInsideStation == null || _playerInsideStation != ballotCollector) return;
        if (ballotCollector.GetBallotCount() == 0)
        {
            Debug.Log("No ballots to dump");
            return;
        }

        // Added: read categorised counts before clearing
        // so we pass the breakdown to VoteManager correctly
        int artists = ballotCollector.GetArtistBallots();
        int nerds = ballotCollector.GetNerdBallots();
        int athletes = ballotCollector.GetAthleteBallots();

        ballotCollector.ClearBallots();

        // Added: pass all three categories to VoteManager
        // instead of a single lump total
        VoteManager.Instance.AddVotesServerRpc(artists, nerds, athletes);
    }
}
