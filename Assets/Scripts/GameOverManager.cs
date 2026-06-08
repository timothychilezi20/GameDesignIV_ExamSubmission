using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
public class GameOverManager : NetworkBehaviour
{

    public static GameOverManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject _winPanel;
    [SerializeField] private GameObject _losePanel;

    [Header("Settings")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    [SerializeField] private float _autoReturnDelay = 10f;

    [Header("Buttons")]
    [SerializeField] private Button _winMenuButton;
    [SerializeField] private Button _loseMenuButton;

    [Header("Particles")]
    [SerializeField] private GameObject _winParticleCanvas;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (_winPanel != null) _winPanel.SetActive(false);
        if (_losePanel != null) _losePanel.SetActive(false);

        if (_winMenuButton != null)
            _winMenuButton.onClick.AddListener(ReturnToMainMenu);

        if (_loseMenuButton != null)
            _loseMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    // Called by RoundManager after round 3 reveal ends
    [ClientRpc]
    public void ShowGameOverClientRpc(bool playersWon)
    {
        Debug.Log($"[GameOverManager] ShowGameOver — playersWon: {playersWon}");

        // Pause the game
        Time.timeScale = 0f;

        if (playersWon)
        {
            if (_winPanel != null) _winPanel.SetActive(true);
            if (_winParticleCanvas != null) _winParticleCanvas.SetActive(true);
            AudioManager.Instance?.PlayMusic(AudioManager.MusicState.RevealSuccess);
        }
        else
        {
            if (_winPanel != null) _winPanel.SetActive(false);
            if (_winParticleCanvas != null) _winParticleCanvas.SetActive(false);
            if (_losePanel != null) _losePanel.SetActive(true);
            AudioManager.Instance?.PlayMusic(AudioManager.MusicState.RevealFailure);
        }

        StartCoroutine(AutoReturnRoutine());
    }

    private IEnumerator AutoReturnRoutine()
    {
        yield return new WaitForSecondsRealtime(_autoReturnDelay);
        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (IsServer)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(_mainMenuSceneName);
    }
}
