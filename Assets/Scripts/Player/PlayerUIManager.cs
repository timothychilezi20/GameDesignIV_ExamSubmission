using UnityEngine;
using Unity.Netcode;

public class PlayerUIManager : NetworkBehaviour
{
    [Header("Player UI Roots")]
    [SerializeField] private GameObject _player1UI; // drag P1 UI parent here
    [SerializeField] private GameObject _player2UI; // drag P2 UI parent here

    // Added: NetworkVariable so all clients know which player
    // number this object represents — 1 for host, 2 for client
    private NetworkVariable<int> _playerNumber = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Reveal Phase")]
    [SerializeField] private GameObject _revealPanel;

    public int _localPlayerNumber = 0;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"PlayerUIManager OnNetworkSpawn — OwnerClientId: {OwnerClientId} | LocalClientId: {NetworkManager.Singleton.LocalClientId} | IsOwner: {IsOwner} | IsServer: {IsServer}");
        // Server assigns player numbers based on client ID
        // Host is always client 0 → Player 1
        // Second player is client 1 → Player 2
        if (IsServer)
        {
            _playerNumber.Value = OwnerClientId == 0 ? 1 : 2;
        }

        // Subscribe so UI updates if player number changes
        _playerNumber.OnValueChanged += OnPlayerNumberAssigned;

        if (_playerNumber.Value != 0)
            OnPlayerNumberAssigned(0, _playerNumber.Value);


        // Apply immediately in case value is already set
        ApplyUI(_playerNumber.Value);

        if (_revealPanel != null)
            _revealPanel.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        _playerNumber.OnValueChanged -= OnPlayerNumberAssigned;
    }

    private void OnPlayerNumberAssigned(int previous, int current)
    {

        if (current == 0) return;


        if (_playerNumber.Value != 0)
            OnPlayerNumberAssigned(0, _playerNumber.Value);


        ApplyUI(current);


    }


    private void ApplyUI(int playerNumber)
    {
        if (playerNumber == 0) return;

        bool isLocalPlayer = OwnerClientId == NetworkManager.Singleton.LocalClientId;

      //  if (isLocalPlayer && _localPlayerNumber == 0)
        //    _localPlayerNumber = playerNumber;

        if (!isLocalPlayer)
        {
            _player1UI.SetActive(false);
            _player2UI.SetActive(false);
            return;
        }


        _player1UI.SetActive(playerNumber == 1);
        _player2UI.SetActive(playerNumber == 2);

        BallotCollector ballotCollector = GetComponent<BallotCollector>();
        if (ballotCollector != null)
        {
            GameObject activeUI = playerNumber == 1 ? _player1UI : _player2UI;
            TMPro.TextMeshPro ballotText = activeUI.GetComponentInChildren<TMPro.TextMeshPro>();
            if (ballotText != null)
                ballotCollector.SetBallotText(ballotText);
        }

        Debug.Log($"Local player is Player {playerNumber}");
    }
    public int GetPlayerNumber() => _localPlayerNumber != 0
       ? _localPlayerNumber
       : _playerNumber.Value;
    public void SetRevealPanel(bool active)
    {
        if (!IsOwner) return;
        if (_revealPanel != null)
            _revealPanel.SetActive(active);
    }

 
}