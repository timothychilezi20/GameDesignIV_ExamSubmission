using UnityEngine;
using UnityEngine.UI;

public class RivalCoupleBarUI : MonoBehaviour
{
    [SerializeField] private Slider rivalProgressBar;
    [SerializeField] private RivalCoupleTimer timer;

    private void Start()
    {
        rivalProgressBar.minValue = 0;
        rivalProgressBar.maxValue = timer.ballotsPerRound * timer.totalRounds;
        rivalProgressBar.value = 0;
    }

    private void Update()
    {
        if (timer == null) return;
        rivalProgressBar.value = timer.GetBarValue();
    }
}