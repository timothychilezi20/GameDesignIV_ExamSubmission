using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject _tutorialPanel;
    [SerializeField] private Image _tutorialImage;
    [SerializeField] private float _autoDismissTime = 5f;

    [Header("Tutorial Images")]
    [SerializeField] private Sprite _rumourFeedPrompt;
    [SerializeField] private Sprite _rivalGoalPrompt;
    [SerializeField] private Sprite _votingStationPrompt;
    [SerializeField] private Sprite _lockInPrompt;
    [SerializeField] private Sprite _gossipersPrompt;

    public enum TutorialType
    {
        RumourFeed,
        RivalGoal,
        VotingStation,
        LockIn,
        Gossipers
    }

    private HashSet<TutorialType> _shownPrompts = new HashSet<TutorialType>();
    private Coroutine _currentCoroutine;
    private bool _isShowing = false;
    private Queue<TutorialType> _promptQueue = new Queue<TutorialType>();

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
        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(false);

        // Show rumour feed prompt at the very start
        ShowPrompt(TutorialType.RumourFeed);
    }

    private void Update()
    {
        // Skip early on any input
        if (_isShowing && Input.anyKeyDown)
            SkipPrompt();
    }

    public void ShowPrompt(TutorialType type)
    {
        // Only show each prompt once
        if (_shownPrompts.Contains(type)) return;

        // Queue it if something is already showing
        if (_isShowing)
        {
            if (!_promptQueue.Contains(type))
                _promptQueue.Enqueue(type);
            return;
        }

        _shownPrompts.Add(type);
        _currentCoroutine = StartCoroutine(DisplayPrompt(type));
    }

    private IEnumerator DisplayPrompt(TutorialType type)
    {
        _isShowing = true;

        Sprite sprite = GetSprite(type);
        if (sprite == null)
        {
            _isShowing = false;
            yield break;
        }

        _tutorialImage.sprite = sprite;
        _tutorialPanel.SetActive(true);

        yield return new WaitForSecondsRealtime(_autoDismissTime);

        HidePanel();
    }

    private void SkipPrompt()
    {
        if (_currentCoroutine != null)
            StopCoroutine(_currentCoroutine);

        HidePanel();
    }

    private void HidePanel()
    {
        _isShowing = false;

        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(false);

        // Show next queued prompt if any
        if (_promptQueue.Count > 0)
        {
            TutorialType next = _promptQueue.Dequeue();
            _shownPrompts.Add(next);
            _currentCoroutine = StartCoroutine(DisplayPrompt(next));
        }
    }

    private Sprite GetSprite(TutorialType type)
    {
        switch (type)

        {
            case TutorialType.RumourFeed: return _rumourFeedPrompt;
            case TutorialType.RivalGoal: return _rivalGoalPrompt;
            case TutorialType.VotingStation: return _votingStationPrompt;
            case TutorialType.LockIn: return _lockInPrompt;
            case TutorialType.Gossipers: return _gossipersPrompt;
            default: return null;
        }
    }
}