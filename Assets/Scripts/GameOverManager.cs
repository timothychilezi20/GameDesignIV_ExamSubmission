using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Summary (optional)")]
    [Tooltip("Optional label to show the winning compatible cliques at game over")]
    [SerializeField] private TextMeshProUGUI _summaryText;

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

        if (_winMenuButton != null) _winMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (_loseMenuButton != null) _loseMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    // Called by RoundManager.CheckWinCondition after all rounds complete
    [ClientRpc]
    public void ShowGameOverClientRpc(bool playersWon)
    {
        Debug.Log($"[GameOverManager] ShowGameOver — playersWon: {playersWon}");

        Time.timeScale = 0f;

        if (_winPanel != null) _winPanel.SetActive(playersWon);
        if (_losePanel != null) _losePanel.SetActive(!playersWon);

        // Show which cliques were on good terms in the final round, if available
        if (_summaryText != null && CliqueRelationshipManager.Instance != null)
        {
            string c1 = CliqueRelationshipManager.Instance.GetGoodTermsClique1().ToString();
            string c2 = CliqueRelationshipManager.Instance.GetGoodTermsClique2().ToString();
            _summaryText.text = $"Final round social dynamic: {c1} + {c2}";
        }

        if (playersWon)
            AudioManager.Instance?.PlayMusic(AudioManager.MusicState.RevealSuccess);
        else
            AudioManager.Instance?.PlayMusic(AudioManager.MusicState.RevealFailure);

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