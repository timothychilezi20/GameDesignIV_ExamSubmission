using UnityEngine;
using UnityEngine.UI;
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

    [Header("Compatible Clique Banner")]
    [Tooltip("Optional label showing which cliques earned the bonus this round")]
    [SerializeField] private TextMeshProUGUI _compatibleCliquesText;
    [Tooltip("Optional panel that highlights the multiplier was active")]
    [SerializeField] private GameObject _multiplierBadge;

    // ─── Clique colours ───────────────────────────────────────────
    // Matches the colours used in RumorFeed for consistency
    private static readonly Color _artistColor = new Color(0.2f, 0.8f, 0.8f); // teal
    private static readonly Color _nerdColor = new Color(0.6f, 0.3f, 0.9f); // purple
    private static readonly Color _athleteColor = new Color(1.0f, 0.5f, 0.1f); // orange
    private static readonly Color _defaultColor = Color.white;
    private static readonly Color _multiplierColor = new Color(1.0f, 0.85f, 0.1f); // gold

    // ──────────────────────────────────────────────────────────────

    // Original signature kept for backward compatibility — called without multiplier info
    public void UpdateScores(
        int p1R1, int p1R2, int p1R3,
        int p2R1, int p2R2, int p2R3,
        int r1R1, int r1R2, int r1R3,
        int r2R1, int r2R2, int r2R3,
        int currentRound)
    {
        UpdateScores(
            p1R1, p1R2, p1R3,
            p2R1, p2R2, p2R3,
            r1R1, r1R2, r1R3,
            r2R1, r2R2, r2R3,
            currentRound,
            false, -1, -1
        );
    }

    // Full signature — includes multiplier state and compatible clique indices
    public void UpdateScores(
        int p1R1, int p1R2, int p1R3,
        int p2R1, int p2R2, int p2R3,
        int r1R1, int r1R2, int r1R3,
        int r2R1, int r2R2, int r2R3,
        int currentRound,
        bool multiplierActive,
        int compatibleClique1,
        int compatibleClique2)
    {
        Debug.Log($"[RevealPanelUI] UpdateScores — Round: {currentRound} | Multiplier: {multiplierActive} | P1R1: {p1R1} | P2R1: {p2R1}");

        // ─── Blue team scores ─────────────────────────────────────
        SetText(_p1Round1Text, currentRound >= 1, p1R1);
        SetText(_p2Round1Text, currentRound >= 1, p2R1);
        SetText(_p1Round2Text, currentRound >= 2, p1R2);
        SetText(_p2Round2Text, currentRound >= 2, p2R2);
        SetText(_p1Round3Text, currentRound >= 3, p1R3);
        SetText(_p2Round3Text, currentRound >= 3, p2R3);

        int blueR1 = p1R1 + p2R1;
        int blueR2 = p1R2 + p2R2;
        int blueR3 = p1R3 + p2R3;
        SetText(_blueTotalRound1Text, currentRound >= 1, blueR1);
        SetText(_blueTotalRound2Text, currentRound >= 2, blueR2);
        SetText(_blueTotalRound3Text, currentRound >= 3, blueR3);
        if (_blueGrandTotalText != null)
            _blueGrandTotalText.text = (blueR1 + blueR2 + blueR3).ToString();

        // ─── Red team scores ──────────────────────────────────────
        SetText(_r1Round1Text, currentRound >= 1, r1R1);
        SetText(_r2Round1Text, currentRound >= 1, r2R1);
        SetText(_r1Round2Text, currentRound >= 2, r1R2);
        SetText(_r2Round2Text, currentRound >= 2, r2R2);
        SetText(_r1Round3Text, currentRound >= 3, r1R3);
        SetText(_r2Round3Text, currentRound >= 3, r2R3);

        int redR1 = r1R1 + r2R1;
        int redR2 = r1R2 + r2R2;
        int redR3 = r1R3 + r2R3;
        SetText(_redTotalRound1Text, currentRound >= 1, redR1);
        SetText(_redTotalRound2Text, currentRound >= 2, redR2);
        SetText(_redTotalRound3Text, currentRound >= 3, redR3);
        if (_redGrandTotalText != null)
            _redGrandTotalText.text = (redR1 + redR2 + redR3).ToString();

        // ─── Multiplier badge & compatible clique banner ──────────
        if (_multiplierBadge != null)
            _multiplierBadge.SetActive(multiplierActive);

        if (_compatibleCliquesText != null)
        {
            if (multiplierActive && compatibleClique1 >= 0 && compatibleClique2 >= 0)
            {
                string name1 = ((CliqueGroup.CliqueType)compatibleClique1).ToString();
                string name2 = ((CliqueGroup.CliqueType)compatibleClique2).ToString();
                Color c1 = GetCliqueColor(compatibleClique1);
                Color c2 = GetCliqueColor(compatibleClique2);
                string hex1 = ColorUtility.ToHtmlStringRGB(c1);
                string hex2 = ColorUtility.ToHtmlStringRGB(c2);
                _compatibleCliquesText.text =
                    $"<color=#{hex1}>{name1}</color> + <color=#{hex2}>{name2}</color> earned <color=#{ColorUtility.ToHtmlStringRGB(_multiplierColor)}>x2</color>";
            }
            else
            {
                _compatibleCliquesText.text = multiplierActive
                    ? "Bonus round — compatible cliques x2"
                    : "No multiplier this round";
            }
        }

        // ─── Tint current-round score cells gold if multiplier ────
        // This gives players a visual cue that their scores for this
        // round were boosted by the compatible clique bonus
        if (multiplierActive)
        {
            TintCurrentRoundScores(currentRound);
        }
        else
        {
            ResetScoreColors();
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private void SetText(TextMeshProUGUI field, bool roundReached, int value)
    {
        if (field == null) return;
        field.text = roundReached ? value.ToString() : "-";
    }

    private Color GetCliqueColor(int cliqueIndex)
    {
        return cliqueIndex switch
        {
            (int)CliqueGroup.CliqueType.Artists => _artistColor,
            (int)CliqueGroup.CliqueType.Nerds => _nerdColor,
            (int)CliqueGroup.CliqueType.Athletes => _athleteColor,
            _ => _defaultColor
        };
    }

    // Tints the score cells for the current round gold to indicate the multiplier fired
    private void TintCurrentRoundScores(int currentRound)
    {
        TextMeshProUGUI[] roundFields = currentRound switch
        {
            1 => new[] { _p1Round1Text, _p2Round1Text, _blueTotalRound1Text },
            2 => new[] { _p1Round2Text, _p2Round2Text, _blueTotalRound2Text },
            3 => new[] { _p1Round3Text, _p2Round3Text, _blueTotalRound3Text },
            _ => null
        };

        if (roundFields == null) return;
        foreach (var field in roundFields)
            if (field != null) field.color = _multiplierColor;
    }

    private void ResetScoreColors()
    {
        TextMeshProUGUI[] allScoreFields = new[]
        {
            _p1Round1Text, _p2Round1Text, _blueTotalRound1Text,
            _p1Round2Text, _p2Round2Text, _blueTotalRound2Text,
            _p1Round3Text, _p2Round3Text, _blueTotalRound3Text
        };
        foreach (var field in allScoreFields)
            if (field != null) field.color = _defaultColor;
    }
}