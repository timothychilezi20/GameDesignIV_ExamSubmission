using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PlayerCoupleBar : NetworkBehaviour
{
    [SerializeField] private Slider playerProgressBar;

    // Instead of a fixed maxBallots, use round-based logic
    [SerializeField] private int ballotsPerRound = 50;
    [SerializeField] private int totalRounds = 3;

    private int maxBallots;
    private BallotCollector _myCollector;

    public override void OnNetworkSpawn()
    {
        // Calculate max ballots same way RivalCoupleTimer does
        maxBallots = ballotsPerRound * totalRounds;

        playerProgressBar.minValue = 0;
        playerProgressBar.maxValue = maxBallots;
        playerProgressBar.value = 0;

        if (!IsOwner) return;

        _myCollector = GetComponent<BallotCollector>()
                    ?? GetComponentInParent<BallotCollector>()
                    ?? GetComponentInChildren<BallotCollector>();

        Debug.Log($"[PlayerCoupleBar] OnNetworkSpawn | collector found: {_myCollector != null}");

        if (_myCollector != null)
            Subscribe();
    }

    public void BindToCollector(BallotCollector collector)
    {
        if (collector == null || !IsOwner) return;

        if (_myCollector != null)
            _myCollector.BallotCountVar.OnValueChanged -= OnBallotsChanged;

        _myCollector = collector;
        Subscribe();

        Debug.Log($"[PlayerCoupleBar] BindToCollector called | value: {_myCollector.BallotCountVar.Value}");
    }

    private void Subscribe()
    {
        // Match max ballots logic
        playerProgressBar.maxValue = ballotsPerRound * totalRounds;

        // Round ballots number like RivalCoupleTimer
        playerProgressBar.value = Mathf.RoundToInt(_myCollector.BallotCountVar.Value);

        _myCollector.BallotCountVar.OnValueChanged += OnBallotsChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (_myCollector != null)
            _myCollector.BallotCountVar.OnValueChanged -= OnBallotsChanged;
    }

    private void OnBallotsChanged(int previous, int current)
    {
        int rounded = Mathf.RoundToInt(current);
        Debug.Log($"[PlayerCoupleBar] {previous} → {rounded}");
        playerProgressBar.value = rounded;
    }
}
