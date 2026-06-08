using UnityEngine;
using Unity.Netcode;

// Tracks per-round ballot collection stats for both players.
// Server writes all values, all clients can read.
// Reset at the start of each round by RoundManager.
public class RoundStats : NetworkBehaviour
{
    public static RoundStats Instance { get; private set; }

    // ─── Player 1 round totals ────────────────────────────────────
    private NetworkVariable<int> _p1Artists = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _p1Nerds = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _p1Athletes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ─── Player 2 round totals ────────────────────────────────────
    private NetworkVariable<int> _p2Artists = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _p2Nerds = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _p2Athletes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Called by BallotCollector on the server when ballots are dumped
    public void RecordBallots(int playerNumber, int artists, int nerds, int athletes)
    {
        if (!IsServer) return;

        if (playerNumber == 1)
        {
            _p1Artists.Value += artists;
            _p1Nerds.Value += nerds;
            _p1Athletes.Value += athletes;
        }
        else if (playerNumber == 2)
        {
            _p2Artists.Value += artists;
            _p2Nerds.Value += nerds;
            _p2Athletes.Value += athletes;
        }

        Debug.Log($"RoundStats — Player {playerNumber} | Artists: {artists} | Nerds: {nerds} | Athletes: {athletes}");
    }

    // Called at the start of each round to wipe per-round stats
    public void ResetForNewRound()
    {
        if (!IsServer) return;
        _p1Artists.Value = 0;
        _p1Nerds.Value = 0;
        _p1Athletes.Value = 0;
        _p2Artists.Value = 0;
        _p2Nerds.Value = 0;
        _p2Athletes.Value = 0;
        Debug.Log("RoundStats reset for new round");
    }

    // ─── Getters ──────────────────────────────────────────────────

    public int GetPlayerArtists(int p) => p == 1 ? _p1Artists.Value : _p2Artists.Value;
    public int GetPlayerNerds(int p) => p == 1 ? _p1Nerds.Value : _p2Nerds.Value;
    public int GetPlayerAthletes(int p) => p == 1 ? _p1Athletes.Value : _p2Athletes.Value;

    public int GetPlayerTotal(int p) =>
        GetPlayerArtists(p) + GetPlayerNerds(p) + GetPlayerAthletes(p);

    public int GetRoundTotal() =>
        GetPlayerTotal(1) + GetPlayerTotal(2);

    // Applies x2 multiplier to good terms cliques for a player
    // Only called during a reveal triggered by lock-in, not timer
    public int GetPlayerScoreWithMultiplier(int playerNumber)
    {
        if (CliqueRelationshipManager.Instance == null) return GetPlayerTotal(playerNumber);

        int artists = GetPlayerArtists(playerNumber);
        int nerds = GetPlayerNerds(playerNumber);
        int athletes = GetPlayerAthletes(playerNumber);

        // Double ballots from cliques on good terms this round
        if (CliqueRelationshipManager.Instance.IsOnGoodTerms(CliqueGroup.CliqueType.Artists))
            artists *= 2;
        if (CliqueRelationshipManager.Instance.IsOnGoodTerms(CliqueGroup.CliqueType.Nerds))
            nerds *= 2;
        if (CliqueRelationshipManager.Instance.IsOnGoodTerms(CliqueGroup.CliqueType.Athletes))
            athletes *= 2;

        return artists + nerds + athletes;
    }
}