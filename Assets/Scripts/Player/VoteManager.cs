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
    [ServerRpc]
    public void AddVotesServerRpc(int artists, int nerds, int athletes)
    {
        _artistVotes.Value += artists;
        _nerdVotes.Value += nerds;
        _athleteVotes.Value += athletes;
        _totalVotes.Value += artists + nerds + athletes;

        Debug.Log($"Votes added — Artists: {artists} | Nerds: {nerds} | Athletes: {athletes}");
        Debug.Log($"Total votes — Artists: {_artistVotes.Value} | Nerds: {_nerdVotes.Value} | Athletes: {_athleteVotes.Value} | Total: {_totalVotes.Value}");
    }

    public int GetTotalVotes() => _totalVotes.Value;
    public int GetArtistVotes() => _artistVotes.Value;
    public int GetNerdVotes() => _nerdVotes.Value;
    public int GetAthleteVotes() => _athleteVotes.Value;
}