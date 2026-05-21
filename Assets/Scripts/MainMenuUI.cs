using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public TMP_InputField joinCodeInput;
    public TMP_Text codeDisplay;
    public TMP_Text statusText;

    public GameObject startButton;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (startButton != null)
            startButton.SetActive(false);

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    // HOST

    public async void OnHostClicked()
    {
        Debug.Log("Host button clicked");

        if (RelayLobbyManager.Instance == null)
        {
            Debug.LogError("RelayLobbyManager.Instance is NULL!");
            return;
        }

        Debug.Log("Relay manager exists");

        string code =
            await RelayLobbyManager.Instance.CreateRelay();

        Debug.Log("Relay created");

        if (codeDisplay == null)
        {
            Debug.LogError("Code Display is NULL!");
            return;
        }

        codeDisplay.text = "JOIN CODE: " + code;

        if (statusText == null)
        {
            Debug.LogError("Status Text is NULL!");
            return;
        }

        statusText.text = "Waiting for player...";

        if (startButton == null)
        {
            Debug.LogError("Start Button is NULL!");
            return;
        }

        startButton.SetActive(true);

        Debug.Log("Everything succeeded");
    }

    // CLIENT

    public async void OnJoinClicked()
    {
        if (string.IsNullOrWhiteSpace(joinCodeInput.text))
        {
            statusText.text = "Enter a code!";
            return;
        }

        statusText.text = "Connecting...";

        await RelayLobbyManager.Instance.JoinRelay(
            joinCodeInput.text
        );

        statusText.text = "Connected!\nWaiting for host...";
    }

    // CONNECTION

    private void OnClientConnected(ulong id)
    {
        Debug.Log("Connected client: " + id);

        if (NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Player Joined!\nReady to start.";
        }
    }

    // START GAME

    public void OnStartGameClicked()
    {
        if (!NetworkManager.Singleton.IsHost)
            return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            statusText.text = "Need 2 players!";
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            "SampleScene",
            LoadSceneMode.Single
        );
    }
}