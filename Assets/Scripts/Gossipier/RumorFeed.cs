using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Attach to the RumorFeed panel inside Player1UI / Player2UI.
// Manages incoming rumor entries — max 4, newest on top,
// oldest fades out after 10 seconds or when pushed off by a 5th.
public class RumorFeed : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _rumorContainer;    // empty GameObject under the title — entries go here
    [SerializeField] private GameObject _rumorEntryPrefab; // prefab with a single TextMeshProUGUI component

    [Header("Settings")]
    [SerializeField] private int _maxRumors = 4;
    [SerializeField] private float _fadeDelay = 10f;       // seconds before oldest rumor fades

    // ─── Clique Area Colours ──────────────────────────────────────
    // Each area name maps to a clique colour.
    // Artists → teal, Nerds → purple, Athletes → orange.
    // Add or adjust colours here to match your game's visual style.
    private static readonly Dictionary<string, Color> _areaColors = new Dictionary<string, Color>
    {
        { "The Art Studio",  new Color(0.2f, 0.8f, 0.8f) },  // teal   — Artists
        { "The Art Corner",  new Color(0.2f, 0.8f, 0.8f) },  // teal   — Artists
        { "The Gym",         new Color(1.0f, 0.5f, 0.1f) },  // orange — Athletes
        { "The Courts",      new Color(1.0f, 0.5f, 0.1f) },  // orange — Athletes
        { "The Labs",        new Color(0.6f, 0.3f, 0.9f) },  // purple — Nerds
        { "Nerd Square",     new Color(0.6f, 0.3f, 0.9f) },  // purple — Nerds
    };

    private Color _defaultColor = Color.white;

    // Tracks active entries oldest → newest
    private List<RumorEntry> _activeEntries = new List<RumorEntry>();

    // ─────────────────────────────────────────────────────────────

    public void AddRumor(string playerLabel, string areaName, bool isInterior)
    {

      //  Debug.Log($"AddRumor called — label: {playerLabel} | area: {areaName} | prefab null: {_rumorEntryPrefab == null} | container null: {_rumorContainer == null}");
        // If at max, remove the oldest immediately
        if (_activeEntries.Count >= _maxRumors)
            RemoveOldest();

        // Build the rumor string with rich text colour on the area name
        Color areaColor = _areaColors.TryGetValue(areaName, out Color c) ? c : _defaultColor;
        string hex = ColorUtility.ToHtmlStringRGB(areaColor);
        string location = isInterior ? $"inside <color=#{hex}>{areaName}</color>"
                                     : $"at <color=#{hex}>{areaName}</color>";
        string rumorText = $"{playerLabel} spotted {location}";

        // Instantiate entry and parent it to the container
        GameObject entryObj = Instantiate(_rumorEntryPrefab, _rumorContainer);

        // Entries stack newest on top — move to first sibling position
        entryObj.transform.SetAsFirstSibling();

        TextMeshProUGUI text = entryObj.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = rumorText;

        RumorEntry entry = new RumorEntry(entryObj, text);
        _activeEntries.Add(entry);

        // Start the fade timer for this entry
        StartCoroutine(FadeRoutine(entry));
    }

    private void RemoveOldest()
    {
        if (_activeEntries.Count == 0) return;

        RumorEntry oldest = _activeEntries[0];
        _activeEntries.RemoveAt(0);
        StopAllCoroutines(); // stop its fade coroutine
        Destroy(oldest.GameObject);

        // Restart fade coroutines for remaining entries
        // since StopAllCoroutines cancelled them all
        foreach (RumorEntry entry in _activeEntries)
            StartCoroutine(FadeRoutine(entry));
    }

    private IEnumerator FadeRoutine(RumorEntry entry)
    {
        // Wait for fade delay
        yield return new WaitForSeconds(_fadeDelay);

        // Only fade if this entry is still the oldest
        if (_activeEntries.Count == 0 || _activeEntries[0] != entry) yield break;

        // Fade out over 1 second
        float elapsed = 0f;
        float fadeDuration = 1f;
        Color startColor = entry.Text.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            entry.Text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        _activeEntries.Remove(entry);
        Destroy(entry.GameObject);
    }

    // Simple container to track each active rumor entry
    private class RumorEntry
    {
        public GameObject GameObject;
        public TextMeshProUGUI Text;

        public RumorEntry(GameObject go, TextMeshProUGUI text)
        {
            GameObject = go;
            Text = text;
        }
    }

    public void AddDirectRumor(string message)
    {
        if (_activeEntries.Count >= _maxRumors)
            RemoveOldest();

        GameObject entryObj = Instantiate(_rumorEntryPrefab, _rumorContainer);
        entryObj.transform.SetAsFirstSibling();

        TextMeshProUGUI text = entryObj.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = message;

        RumorEntry entry = new RumorEntry(entryObj, text);
        _activeEntries.Add(entry);
        StartCoroutine(FadeRoutine(entry));
    }
}