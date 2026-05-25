using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class ForceUppercaseTMP : MonoBehaviour
{
    private TMP_InputField inputField;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
    }

    private void Update()
    {
        string upper = inputField.text.ToUpper();

        if (inputField.text != upper)
        {
            int caret = inputField.caretPosition;

            inputField.text = upper;

            inputField.caretPosition = caret;
        }
    }
}