using UnityEngine;
using Unity.Netcode;

public class PlayerSpawnManager : NetworkBehaviour
{
    [SerializeField] private Transform[] _spawnPoints; // Assign P1 and P2 points in Inspector

    public override void OnNetworkSpawn()
    {
        // Only the server controls spawning
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Host is already connected by the time OnNetworkSpawn fires
        // so we manually handle the host's spawn here
        SpawnPlayerAtPoint(NetworkManager.Singleton.LocalClientId);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        // Skip the host — already handled in OnNetworkSpawn
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        SpawnPlayerAtPoint(clientId);
    }

    private void SpawnPlayerAtPoint(ulong clientId)
    {
        // Client 0 = host → spawn point 0
        // Client 1 = second player → spawn point 1
        // Clamp so it never goes out of bounds if more clients connect
        int spawnIndex = Mathf.Clamp((int)clientId, 0, _spawnPoints.Length - 1);
        Transform spawnPoint = _spawnPoints[spawnIndex];

        // Grab the already-spawned player object NGO created
        // and move it to the correct spawn point
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.transform.position = spawnPoint.position;
                client.PlayerObject.transform.rotation = spawnPoint.rotation;
            }
        }
    }
}