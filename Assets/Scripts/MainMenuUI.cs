using TMPro;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject titleScreenPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject clientPanel;

    [Header("Host UI")]
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private GameObject startGameButton;

    [Header("Client UI")]
    [SerializeField] private TMP_InputField joinCodeInput;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    void Start()
    {
        ShowTitleScreen();
    }

    private void ResetUI()
    {
        // Disable all panels
        titleScreenPanel.SetActive(false);
        controlsPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        hostPanel.SetActive(false);
        clientPanel.SetActive(false);

        // Clear text
        statusText.text = string.Empty;
        joinCodeText.text = string.Empty;
        joinCodeInput.text = string.Empty;

        // Hide status text
        statusText.gameObject.SetActive(false);

        // Reset buttons
        startGameButton.SetActive(false);
    }

    // =====================================================
    // TITLE SCREEN
    // =====================================================

    public void OpenLobby()
    {
        ResetUI();

        lobbyPanel.SetActive(true);
    }

    public void OpenControls()
    {
        ResetUI();

        controlsPanel.SetActive(true);
    }

    // =====================================================
    // HOST
    // =====================================================

    public async void OnHostPressed()
    {
        ResetUI();

        hostPanel.SetActive(true);

        string code =
            await RelayLobbyManager.Instance.CreateRelay();

        joinCodeText.text = code;

        statusText.text = "Waiting for player...";

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    // =====================================================
    // JOIN
    // =====================================================

    public void OnJoinPressed()
    {
        ResetUI();

        clientPanel.SetActive(true);
    }

    public async void OnConnectPressed()
    {
        string code = joinCodeInput.text.ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            statusText.text = "Enter a code!";
            return;
        }

        await RelayLobbyManager.Instance.JoinRelay(code);

        statusText.text =
            "Connected!";
    }

    private void OnClientConnected(ulong clientId)
    {
        // Ignore host connecting to itself
        if (clientId == NetworkManager.Singleton.LocalClientId)
            return;

        statusText.text = "Player connected! Ready to start.";

        startGameButton.SetActive(true);
    }

    // =====================================================
    // START GAME
    // =====================================================

    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost)
            return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            statusText.text =
                "Waiting for second player...";
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            "Apayin",
            LoadSceneMode.Single
        );
    }

    // =====================================================
    // BACK BUTTON
    // =====================================================

    public void BackToTitle()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost ||
             NetworkManager.Singleton.IsClient))
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        ShowTitleScreen();
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private void ShowTitleScreen()
    {
        ResetUI();

        titleScreenPanel.SetActive(true);
    }
}