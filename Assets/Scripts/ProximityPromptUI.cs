using UnityEngine;
using TMPro;

public class ProximityPromptUI : MonoBehaviour
{
    public static ProximityPromptUI Instance { get; private set; }

    [SerializeField] private GameObject _promptPanel;
    [SerializeField] private TextMeshProUGUI _promptText;

    private CliqueGroup _currentOwner = null;

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
        _currentOwner = null;
        _promptPanel.SetActive(false);
    }

    public void ShowPrompt(string message, CliqueGroup owner)
    {
        _currentOwner = owner;
        _promptPanel.SetActive(true);
        if (_promptText != null)
            _promptText.text = message;
    }

    public void HidePrompt(CliqueGroup requester)
    {
        // Only hide if the requester is the one currently showing the prompt
        if (requester != null && requester != _currentOwner) return;

        _currentOwner = null;
        _promptPanel.SetActive(false);
    }
}