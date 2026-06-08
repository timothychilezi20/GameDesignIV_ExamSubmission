using Unity.Netcode;
using UnityEngine;

public class VoteManager : NetworkBehaviour
{
    public static VoteManager Instance { get; private set; }

    // ─── Global clique totals (cumulative across all rounds) ──────
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

    // ─── Per-player cumulative totals ─────────────────────────────
    // Used by RoundManager.SnapshotRoundVotes to calculate round deltas
    private NetworkVariable<int> _player1Votes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _player2Votes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // ─── Per-player compatible vote counters (reset each round) ───
    // Tracks votes from cliques on good terms this round.
    // Reset by ResetRoundVotes at the end of each reveal so the
    // next round starts from zero — cumulative totals above stay intact.
    private NetworkVariable<int> _player1CompatibleVotes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _player2CompatibleVotes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─── Vote Reception ───────────────────────────────────────────

    // Called on the server by BallotCollector's ServerRpc.
    // compatibleArtists/Nerds/Athletes are the subset of the dumped
    // ballots that came from cliques currently on good terms —
    // used by SnapshotRoundVotes to apply the x2 multiplier correctly.
    public void ReceiveVotes(
        int artists, int nerds, int athletes,
        int playerNumber,
        int compatibleArtists, int compatibleNerds, int compatibleAthletes)
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

        Debug.Log($"[VoteManager] ReceiveVotes — Player: {playerNumber} | " +
                  $"Artists: {artists} | Nerds: {nerds} | Athletes: {athletes} | " +
                  $"Total: {total} | Compatible: {compatibleTotal}");
        Debug.Log($"[VoteManager] Running totals — P1: {_player1Votes.Value} | " +
                  $"P2: {_player2Votes.Value} | Grand total: {_totalVotes.Value}");
    }

    // ─── Round Reset ──────────────────────────────────────────────

    // Called by RoundManager at the end of each reveal.
    // Only clears compatible counters — cumulative totals stay intact
    // so SnapshotRoundVotes can subtract the round-start snapshot correctly.
    public void ResetRoundVotes()
    {
        if (!IsServer) return;

        _player1CompatibleVotes.Value = 0;
        _player2CompatibleVotes.Value = 0;

        Debug.Log("[VoteManager] ResetRoundVotes — compatible counters cleared for new round");
    }

    // ─── Getters ──────────────────────────────────────────────────

    public int GetTotalVotes() => _totalVotes.Value;
    public int GetArtistVotes() => _artistVotes.Value;
    public int GetNerdVotes() => _nerdVotes.Value;
    public int GetAthleteVotes() => _athleteVotes.Value;

    public int GetPlayer1Votes() => _player1Votes.Value;
    public int GetPlayer2Votes() => _player2Votes.Value;

    public int GetPlayer1CompatibleVotes() => _player1CompatibleVotes.Value;
    public int GetPlayer2CompatibleVotes() => _player2CompatibleVotes.Value;
}

