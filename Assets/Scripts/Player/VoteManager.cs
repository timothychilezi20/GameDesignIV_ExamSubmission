using UnityEngine;
using Unity.Netcode;

public class VoteManager : NetworkBehaviour
{
    public static VoteManager Instance { get; private set; }

    // Added: separate NetworkVariables per clique type.
    // All server-write so only the server updates them,
    // but all clients can read to display totals.
    private NetworkVariable<int> _totalVotes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _artistVotes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _nerdVotes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _athleteVotes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _player1Votes = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
);
    private NetworkVariable<int> _player2Votes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _player1CompatibleVotes = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
);
    private NetworkVariable<int> _player2CompatibleVotes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public void ReceiveVotes(int artists, int nerds, int athletes, int playerNumber, int compatibleArtists, int compatibleNerds, int compatibleAthletes)
    {
        if (!IsServer) return;

        int total = artists + nerds + athletes;
        int compatibleTotal = compatibleArtists + compatibleNerds + compatibleAthletes;

        _artistVotes.Value += artists;
        _nerdVotes.Value += nerds;
        _athleteVotes.Value += athletes;
        _totalVotes.Value += total;

        if (playerNumber == 1)
        {
            _player1Votes.Value += total;
            _player1CompatibleVotes.Value += compatibleTotal;
        }
        else if (playerNumber == 2)
        {
            _player2Votes.Value += total;
            _player2CompatibleVotes.Value += compatibleTotal;
        }

        Debug.Log($"[VoteManager] ReceiveVotes — Player: {playerNumber} | Total: {total} | Compatible: {compatibleTotal}");
    }

    public int GetPlayer1CompatibleVotes() => _player1CompatibleVotes.Value;
    public int GetPlayer2CompatibleVotes() => _player2CompatibleVotes.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Added: categorised ServerRpc — takes all three clique counts
    // separately so votes are stored by type, not just as a lump sum.
    // RequireOwnership = false so either client can call it.


    // Called directly on the server by BallotCollector's ServerRpc
    // No ownership needed since this runs on the server already
    public void ReceiveVotes(int artists, int nerds, int athletes, int playerNumber)
    {
        if (!IsServer) return;

        Debug.Log($"[VoteManager] ReceiveVotes — Player: {playerNumber} | Artists: {artists} | Nerds: {nerds} | Athletes: {athletes}");
        Debug.Log($"[VoteManager] P1 total: {_player1Votes.Value} | P2 total: {_player2Votes.Value}");

        int total = artists + nerds + athletes;

        _artistVotes.Value += artists;
        _nerdVotes.Value += nerds;
        _athleteVotes.Value += athletes;
        _totalVotes.Value += total;

        if (playerNumber == 1)
            _player1Votes.Value += total;
        else if (playerNumber == 2)
            _player2Votes.Value += total;

        Debug.Log($"Votes received — Artists: {artists} | Nerds: {nerds} | Athletes: {athletes} | Player: {playerNumber}");
        Debug.Log($"Total — P1: {_player1Votes.Value} | P2: {_player2Votes.Value} | Total: {_totalVotes.Value}");
    }

    public void ResetRoundVotes()
    {
        if (!IsServer) return;
        // Only reset compatible vote counters — totals stay cumulative for delta calculation
        _player1CompatibleVotes.Value = 0;
        _player2CompatibleVotes.Value = 0;
        Debug.Log("[VoteManager] Round votes reset — compatible counters cleared");
    }

    public int GetTotalVotes() => _totalVotes.Value;
    public int GetArtistVotes() => _artistVotes.Value;
    public int GetNerdVotes() => _nerdVotes.Value;
    public int GetAthleteVotes() => _athleteVotes.Value;
    public int GetPlayer1Votes() => _player1Votes.Value;
    public int GetPlayer2Votes() => _player2Votes.Value;
}