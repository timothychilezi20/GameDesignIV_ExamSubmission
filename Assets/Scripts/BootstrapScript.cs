using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;

public class UGSBootstrap : MonoBehaviour
{
    private async void Awake()
    {
        await InitializeUGS();
    }

    private async Task InitializeUGS()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Debug.Log("UGS Initialized + Signed In");
    }
}