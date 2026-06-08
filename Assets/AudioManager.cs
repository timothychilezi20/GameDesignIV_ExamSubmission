using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer _audioMixer;

    [Header("Music Sources")]
    [SerializeField] private AudioSource _musicSource;
    [SerializeField] private AudioSource _musicSourceB; // for crossfading

    [Header("SFX Source")]
    [SerializeField] private AudioSource _sfxSource;

    [Header("Exploration Music")]
    [SerializeField] private AudioClip _explorationMusic;

    [Header("Danger Music")]
    [SerializeField] private AudioClip _dangerMusic;

    [Header("Reveal Music")]
    [SerializeField] private AudioClip _revealBuildupMusic;

    [Header("Reveal Outcome")]
    [SerializeField] private AudioClip _successMusic;
    [SerializeField] private AudioClip _failureMusic;

    [Header("SFX")]
    [SerializeField] private AudioClip _rumourPingSFX;
    [SerializeField] private AudioClip _ballotCollectSFX;
    [SerializeField] private AudioClip _ballotDumpSFX;
    [SerializeField] private AudioClip _lockInSFX;

    [Header("Ambience")]
    [SerializeField] private AudioSource _ambienceSource;
    [SerializeField] private AudioClip _ambienceClip;


    [Header("Settings")]
    [SerializeField] private float _crossfadeDuration = 1.5f;

    [Header("Main Menu")]
    [SerializeField] private AudioClip _mainMenuMusic;

    [Header("UI SFX")]
    [SerializeField] private AudioClip _buttonClickSFX;

    [Header("Scene Settings")]
    [SerializeField] private bool _isMainMenu = false;

    public enum MusicState
    {
        Exploration,
        Danger,
        RevealBuildup,
        RevealSuccess,
        RevealFailure,
        MainMenu
    }

    private MusicState _currentState = MusicState.Exploration;
    private bool _inDangerZone = false;
    private Coroutine _crossfadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-create AudioSources if not assigned
        AudioSource[] sources = GetComponents<AudioSource>();

        if (_musicSource == null)
            _musicSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        if (_musicSourceB == null)
            _musicSourceB = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

        if (_sfxSource == null)
            _sfxSource = sources.Length > 2 ? sources[2] : gameObject.AddComponent<AudioSource>();

        if (_ambienceSource == null)
            _ambienceSource = sources.Length > 3 ? sources[3] : gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        if (_ambienceSource != null && _ambienceClip != null)
        {
            _ambienceSource.clip = _ambienceClip;
            _ambienceSource.loop = true;
            _ambienceSource.Play();
        }

        if (_isMainMenu)
            PlayMusic(MusicState.MainMenu);
        else
            PlayMusic(MusicState.Exploration);
    }

    // =========================
    // MUSIC
    // =========================

    public void PlayMusic(MusicState state)
    {
        if (_currentState == state) return;
        _currentState = state;

        AudioClip clip = GetMusicClip(state);
        if (clip == null) return;

        if (_crossfadeCoroutine != null)
            StopCoroutine(_crossfadeCoroutine);

        _crossfadeCoroutine = StartCoroutine(CrossfadeMusic(clip));
    }

    private AudioClip GetMusicClip(MusicState state)
    {
        switch (state)
        {
            case MusicState.Exploration: return _explorationMusic;
            case MusicState.Danger: return _dangerMusic;
            case MusicState.RevealBuildup: return _revealBuildupMusic;
            case MusicState.RevealSuccess: return _successMusic;
            case MusicState.RevealFailure: return _failureMusic;
                case MusicState.MainMenu: return _mainMenuMusic;
            default: return null;
        }
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        if (_musicSourceB == null)
        {
            // No crossfade source — just switch directly
            _musicSource.clip = newClip;
            _musicSource.loop = true;
            _musicSource.Play();
            yield break;
        }

        // Set up secondary source with new clip
        _musicSourceB.clip = newClip;
        _musicSourceB.volume = 0f;
        _musicSourceB.loop = true;
        _musicSourceB.Play();

        float elapsed = 0f;
        float startVolume = _musicSource.volume;

        while (elapsed < _crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / _crossfadeDuration;

            _musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            _musicSourceB.volume = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        // Swap sources
        _musicSource.Stop();
        _musicSource.clip = newClip;
        _musicSource.volume = 1f;
        _musicSource.loop = true;
        _musicSource.Play();

        _musicSourceB.Stop();
        _musicSourceB.volume = 0f;
    }

    // =========================
    // DANGER ZONE
    // =========================

    public void EnterDangerZone()
    {
        if (_inDangerZone) return;
        _inDangerZone = true;

        if (_currentState == MusicState.Exploration)
            PlayMusic(MusicState.Danger);
    }

    public void ExitDangerZone()
    {
        if (!_inDangerZone) return;
        _inDangerZone = false;

        if (_currentState == MusicState.Danger)
            PlayMusic(MusicState.Exploration);
    }

    // =========================
    // REVEAL
    // =========================

    public void PlayRevealBuildup()
    {
        PlayMusic(MusicState.RevealBuildup);
    }

    public void PlayRevealOutcome(bool success)
    {
        PlayMusic(success ? MusicState.RevealSuccess : MusicState.RevealFailure);
    }

    // =========================
    // SFX
    // =========================

    public void PlayRumourPing()
    {
        PlaySFX(_rumourPingSFX);
    }

    public void PlayBallotCollect()
    {
        PlaySFX(_ballotCollectSFX);
    }

    public void PlayBallotDump()
    {
        PlaySFX(_ballotDumpSFX);
    }

    public void PlayLockIn()
    {
        PlaySFX(_lockInSFX);
    }

    public void PlayButtonClick()
    {
        PlaySFX(_buttonClickSFX);
    }

    private void PlaySFX(AudioClip clip)
    {
        Debug.Log($"[AudioManager] PlaySFX called | clip null: {clip == null} | _sfxSource null: {_sfxSource == null}");
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip);
    }
}