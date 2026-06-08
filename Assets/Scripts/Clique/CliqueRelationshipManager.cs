using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

// Manages which two cliques are on good terms each round.
// Randomises the pairing at the start of each round.
// Good terms means ballots from both cliques get x2 multiplier
// during a reveal phase triggered by player lock-in.
public class CliqueRelationshipManager : NetworkBehaviour
{
    public static CliqueRelationshipManager Instance { get; private set; }

    // The two cliques currently on good terms this round
    // Server writes, all clients read
    private NetworkVariable<int> _goodTermsClique1 = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int> _goodTermsClique2 = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
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

    // Called by RoundManager at the start of each round
    public void RandomiseRelationshipsForRound(int roundNumber)
    {
        if (!IsServer) return;

        // Three cliques: 0=Artists, 1=Nerds, 2=Athletes
        // Pick two different cliques to be on good terms
        List<int> pool = new List<int> { 0, 1, 2 };
        int indexA = Random.Range(0, pool.Count);
        int cliqueA = pool[indexA];
        pool.RemoveAt(indexA);
        int cliqueB = pool[Random.Range(0, pool.Count)];

        _goodTermsClique1.Value = cliqueA;
        _goodTermsClique2.Value = cliqueB;

        string nameA = ((CliqueGroup.CliqueType)cliqueA).ToString();
        string nameB = ((CliqueGroup.CliqueType)cliqueB).ToString();
        Debug.Log($"Round {roundNumber} — Good terms: {nameA} and {nameB}");

        NotifyRelationshipClientRpc(cliqueA, cliqueB, roundNumber);
    }

    [ClientRpc]
    private void NotifyRelationshipClientRpc(int cliqueA, int cliqueB, int roundNumber)
    {
        string nameA = ((CliqueGroup.CliqueType)cliqueA).ToString();
        string nameB = ((CliqueGroup.CliqueType)cliqueB).ToString();
        Debug.Log($"Round {roundNumber} social dynamic — {nameA} and {nameB} are on good terms");
    }

    public bool IsOnGoodTerms(CliqueGroup.CliqueType type)
    {
        int typeInt = (int)type;
        return typeInt == _goodTermsClique1.Value || typeInt == _goodTermsClique2.Value;
    }

    public CliqueGroup.CliqueType GetGoodTermsClique1() => (CliqueGroup.CliqueType)_goodTermsClique1.Value;
    public CliqueGroup.CliqueType GetGoodTermsClique2() => (CliqueGroup.CliqueType)_goodTermsClique2.Value;
}
