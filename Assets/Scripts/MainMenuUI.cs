using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using Unity.VisualScripting;

public class MainMenuUI : MonoBehaviour
{
    public TMP_InputField joinCodeInput;
    public TMP_Text codeDisplay; 

    public async void OnHostClicked()
    {
        string code = await RelayLobbyManager.Instance.CreateRelay();
        codeDisplay.text = "Code" + code;
    }

    public async void OnJoinClicked()
    {
        await RelayLobbyManager.Instance.JoinRelay(joinCodeInput.text);
    }
}
