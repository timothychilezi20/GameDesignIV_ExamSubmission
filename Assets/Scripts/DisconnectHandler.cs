using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DisconnectHandler : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject disconnectPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Button homeButton;

    [Header("Settings")]
    [SerializeField] private float reconnectWindow = 10f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool _disconnectHandled = false;

    private void Start()
    {
        disconnectPanel.SetActive(false);
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomePressed);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (_disconnectHandled) return;
        _disconnectHandled = true;

        disconnectPanel.SetActive(true);
        StartCoroutine(CountdownAndReturn());
    }

    private IEnumerator CountdownAndReturn()
    {
        float remaining = reconnectWindow;

        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = $"Returning to menu in {Mathf.CeilToInt(remaining)}s...";

            remaining -= Time.deltaTime;
            yield return null;
        }

        ReturnToMenu();
    }

    private void OnHomePressed()
    {
        StopAllCoroutines();
        ReturnToMenu();
    }

    private void ReturnToMenu()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}