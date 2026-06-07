using UnityEngine;
using Unity.Netcode;
using System.Collections;

// Singleton NetworkBehaviour managing the round structure.
// Tracks which players have locked in, pauses both games
// when both are locked, shows the reveal panel, then resumes.
// Place in scene alongside VoteManager and RumorManager.
public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Reveal Settings")]
    [SerializeField] private float _revealDuration = 30f;

    // Tracks lock-in state for each player — server writes, all read
    private NetworkVariable<bool> _player1LockedIn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _player2LockedIn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _revealActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
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

    public override void OnNetworkSpawn()
    {
        // Subscribe to reveal state changes so all clients
        // can show/hide the reveal panel reactively
        _revealActive.OnValueChanged += OnRevealStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _revealActive.OnValueChanged -= OnRevealStateChanged;
    }

    // Called by BallotCollector when a player finishes the lock-in hold
    [ServerRpc]
    public void LockInVotesServerRpc(int playerNumber)
    {

        Debug.Log($"LockInVotesServerRpc received — playerNumber: {playerNumber}");
        if (playerNumber == 1)
            _player1LockedIn.Value = true;
        else if (playerNumber == 2)
            _player2LockedIn.Value = true;

        Debug.Log($"Lock state — P1: {_player1LockedIn.Value} | P2: {_player2LockedIn.Value}");

        // Notify the other player via rumor feed
        NotifyLockInClientRpc(playerNumber);

        // Check if both players have locked in
        if (_player1LockedIn.Value && _player2LockedIn.Value)
        {
            Debug.Log("Both locked in — starting reveal");
            StartCoroutine(RevealSequence());
        }
    }

    // Sends lock-in rumor to the other player's feed
    [ClientRpc]
    private void NotifyLockInClientRpc(int lockedInPlayerNumber)
    {
        int recipientPlayerNumber = lockedInPlayerNumber == 1 ? 2 : 1;

        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            if (uiManager.GetPlayerNumber() != recipientPlayerNumber) continue;

            RumorFeed feed = uiManager.GetComponentInChildren<RumorFeed>(true);
            if (feed == null) continue;

            feed.AddDirectRumor($"Player {lockedInPlayerNumber} has locked in their ballots");
        }
    }

    private IEnumerator RevealSequence()
    {
        _revealActive.Value = true;
        Debug.Log("Reveal phase started");

        // WaitForSecondsRealtime ignores timeScale so the coroutine
        // still completes even when the game is paused
        yield return new WaitForSecondsRealtime(_revealDuration);

        _revealActive.Value = false;
        _player1LockedIn.Value = false;
        _player2LockedIn.Value = false;

        Debug.Log("Reveal phase ended — resuming game");
    }

    private void OnRevealStateChanged(bool previous, bool current)
    {
        Debug.Log($"OnRevealStateChanged — active: {current} | IsOwner: {IsOwner}");
        // Pause or resume local game based on reveal state
        Time.timeScale = current ? 0f : 1f;

        // Show or hide the reveal panel on the local player's UI
        PlayerUIManager[] allPlayers = FindObjectsByType<PlayerUIManager>(FindObjectsSortMode.None);
        foreach (PlayerUIManager uiManager in allPlayers)
        {
            if (!uiManager.IsOwner) continue;
            uiManager.SetRevealPanel(current);
        }
    }

    public bool IsRevealActive => _revealActive.Value;
    public bool IsPlayerLockedIn(int playerNumber)
    {
        return playerNumber == 1 ? _player1LockedIn.Value : _player2LockedIn.Value;
    }
}
