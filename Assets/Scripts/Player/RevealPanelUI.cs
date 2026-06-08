using UnityEngine;
using TMPro;

public class RevealPanelUI : MonoBehaviour
{
    [Header("Blue Team - Individual Scores")]
    [SerializeField] private TextMeshProUGUI _p1Round1Text;
    [SerializeField] private TextMeshProUGUI _p2Round1Text;
    [SerializeField] private TextMeshProUGUI _p1Round2Text;
    [SerializeField] private TextMeshProUGUI _p2Round2Text;
    [SerializeField] private TextMeshProUGUI _p1Round3Text;
    [SerializeField] private TextMeshProUGUI _p2Round3Text;

    [Header("Blue Team - Round Totals")]
    [SerializeField] private TextMeshProUGUI _blueTotalRound1Text;
    [SerializeField] private TextMeshProUGUI _blueTotalRound2Text;
    [SerializeField] private TextMeshProUGUI _blueTotalRound3Text;
    [SerializeField] private TextMeshProUGUI _blueGrandTotalText;

    [Header("Red Team - Individual Scores")]
    [SerializeField] private TextMeshProUGUI _r1Round1Text;
    [SerializeField] private TextMeshProUGUI _r2Round1Text;
    [SerializeField] private TextMeshProUGUI _r1Round2Text;
    [SerializeField] private TextMeshProUGUI _r2Round2Text;
    [SerializeField] private TextMeshProUGUI _r1Round3Text;
    [SerializeField] private TextMeshProUGUI _r2Round3Text;

    [Header("Red Team - Round Totals")]
    [SerializeField] private TextMeshProUGUI _redTotalRound1Text;
    [SerializeField] private TextMeshProUGUI _redTotalRound2Text;
    [SerializeField] private TextMeshProUGUI _redTotalRound3Text;
    [SerializeField] private TextMeshProUGUI _redGrandTotalText;

    public void UpdateScores(
        int p1R1, int p1R2, int p1R3,
        int p2R1, int p2R2, int p2R3,
        int r1R1, int r1R2, int r1R3,
        int r2R1, int r2R2, int r2R3,
        int currentRound)
    {
        Debug.Log($"[RevealPanelUI] UpdateScores called — Round: {currentRound} | P1R1: {p1R1} | P2R1: {p2R1}");

        _p1Round1Text.text = currentRound >= 1 ? p1R1.ToString() : "-";
        _p2Round1Text.text = currentRound >= 1 ? p2R1.ToString() : "-";
        _p1Round2Text.text = currentRound >= 2 ? p1R2.ToString() : "-";
        _p2Round2Text.text = currentRound >= 2 ? p2R2.ToString() : "-";
        _p1Round3Text.text = currentRound >= 3 ? p1R3.ToString() : "-";
        _p2Round3Text.text = currentRound >= 3 ? p2R3.ToString() : "-";

        int blueR1 = p1R1 + p2R1;
        int blueR2 = p1R2 + p2R2;
        int blueR3 = p1R3 + p2R3;
        _blueTotalRound1Text.text = currentRound >= 1 ? blueR1.ToString() : "-";
        _blueTotalRound2Text.text = currentRound >= 2 ? blueR2.ToString() : "-";
        _blueTotalRound3Text.text = currentRound >= 3 ? blueR3.ToString() : "-";
        _blueGrandTotalText.text = (blueR1 + blueR2 + blueR3).ToString();

        _r1Round1Text.text = currentRound >= 1 ? r1R1.ToString() : "-";
        _r2Round1Text.text = currentRound >= 1 ? r2R1.ToString() : "-";
        _r1Round2Text.text = currentRound >= 2 ? r1R2.ToString() : "-";
        _r2Round2Text.text = currentRound >= 2 ? r2R2.ToString() : "-";
        _r1Round3Text.text = currentRound >= 3 ? r1R3.ToString() : "-";
        _r2Round3Text.text = currentRound >= 3 ? r2R3.ToString() : "-";

        int redR1 = r1R1 + r2R1;
        int redR2 = r1R2 + r2R2;
        int redR3 = r1R3 + r2R3;
        _redTotalRound1Text.text = currentRound >= 1 ? redR1.ToString() : "-";
        _redTotalRound2Text.text = currentRound >= 2 ? redR2.ToString() : "-";
        _redTotalRound3Text.text = currentRound >= 3 ? redR3.ToString() : "-";
        _redGrandTotalText.text = (redR1 + redR2 + redR3).ToString();
    }
}