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

    // =====================================================
    // TITLE SCREEN
    // =====================================================

    public void OpenLobby()
    {
        titleScreenPanel.SetActive(false);

        controlsPanel.SetActive(false);
        hostPanel.SetActive(false);
        clientPanel.SetActive(false);

        lobbyPanel.SetActive(true);
    }

    public void OpenControls()
    {
        titleScreenPanel.SetActive(false);

        lobbyPanel.SetActive(false);
        hostPanel.SetActive(false);
        clientPanel.SetActive(false);

        controlsPanel.SetActive(true);
    }

    // =====================================================
    // HOST
    // =====================================================

    public async void OnHostPressed()
    {
        lobbyPanel.SetActive(false);
        hostPanel.SetActive(true);

        string code =
            await RelayLobbyManager.Instance.CreateRelay();

        joinCodeText.text =
            "" + code;

        startGameButton.SetActive(true);

        statusText.text =
            "Waiting for player...";
    }

    // =====================================================
    // JOIN
    // =====================================================

    public void OnJoinPressed()
    {
        lobbyPanel.SetActive(false);
        clientPanel.SetActive(true);
    }

    public async void OnConnectPressed()
    {
        string code = joinCodeInput.text;

        if (string.IsNullOrEmpty(code))
        {
            statusText.text = "Enter a code!";
            return;
        }

        await RelayLobbyManager.Instance.JoinRelay(code);

        statusText.text =
            "Connected!";
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
            "SampleScene",
            LoadSceneMode.Single
        );
    }

    // =====================================================
    // BACK BUTTON
    // =====================================================

    public void BackToTitle()
    {
        // If currently connected, disconnect safely
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost ||
             NetworkManager.Singleton.IsClient))
        {
            NetworkManager.Singleton.Shutdown();
        }

        ShowTitleScreen();
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private void ShowTitleScreen()
    {
        titleScreenPanel.SetActive(true);

        controlsPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        hostPanel.SetActive(false);
        clientPanel.SetActive(false);

        joinCodeInput.text = "";
        joinCodeText.text = "";

        statusText.text = "";
    }
}