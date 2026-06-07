using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI; 

public class PlayerUIManager : NetworkBehaviour
{
    [Header("Player UI Roots")]
    [SerializeField] private GameObject _player1UI;
    [SerializeField] private GameObject _player2UI;

    [Header("Ballot UI")]
    [SerializeField] private Canvas _ballotCanvas;
    [SerializeField] private TextMeshProUGUI _player1BallotText;
    [SerializeField] private TextMeshProUGUI _player2BallotText;
    [SerializeField] private Image _player1BallotBackground;
    [SerializeField] private Image _player2BallotBackground;

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
            _playerNumber.Value = OwnerClientId == 0 ? 1 : 2;

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
            // Hide the entire ballot canvas for non-owners
            if (_ballotCanvas != null)
                _ballotCanvas.gameObject.SetActive(false);
            return;
        }


        _player1UI.SetActive(playerNumber == 1);
        _player2UI.SetActive(playerNumber == 2);

        if (_ballotCanvas != null)
            _ballotCanvas.gameObject.SetActive(true);

        if (_player1BallotBackground != null)
            _player1BallotBackground.gameObject.SetActive(playerNumber == 1);

        if (_player2BallotBackground != null)
            _player2BallotBackground.gameObject.SetActive(playerNumber == 2);

        BallotCollector ballotCollector = GetComponent<BallotCollector>();
        if (ballotCollector != null)
        {
            TextMeshProUGUI ballotText = playerNumber == 1 ? _player1BallotText : _player2BallotText;
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