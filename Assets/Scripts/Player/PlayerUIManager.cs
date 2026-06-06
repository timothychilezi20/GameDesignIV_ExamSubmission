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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            _playerNumber.Value = OwnerClientId == 0 ? 1 : 2;

        _playerNumber.OnValueChanged += OnPlayerNumberAssigned;
        ApplyUI(_playerNumber.Value);
    }

    public override void OnNetworkDespawn()
    {
        _playerNumber.OnValueChanged -= OnPlayerNumberAssigned;
    }

    private void OnPlayerNumberAssigned(int previous, int current)
    {
        ApplyUI(current);
    }

    private void ApplyUI(int playerNumber)
    {
        if (playerNumber == 0) return;

        if (!IsOwner)
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

    public int GetPlayerNumber() => _playerNumber.Value;
}