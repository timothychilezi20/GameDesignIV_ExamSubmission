using UnityEngine;
using UnityEngine.UI;

public class ButtonClickSound : MonoBehaviour
{

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
            AudioManager.Instance?.PlayButtonClick());
    }
}