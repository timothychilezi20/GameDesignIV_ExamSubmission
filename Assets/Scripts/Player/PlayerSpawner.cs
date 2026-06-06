using UnityEngine;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using System.Collections;

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
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        // Changed: wrap in coroutine to wait for PlayerObject to be ready.
        // OnClientConnected fires as soon as the client connects but NGO
        // spawns the player object slightly after — polling until it exists
        // guarantees we never try to reposition a null PlayerObject.
        StartCoroutine(WaitAndSpawn(clientId));
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

    private IEnumerator WaitAndSpawn(ulong clientId)
    {
        // Poll until the player object exists — usually resolves in 1-2 frames
        NetworkClient client;
        while (true)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out client))
            {
                if (client.PlayerObject != null) break;
            }
            yield return null;
        }

        int spawnIndex = Mathf.Clamp((int)clientId, 0, _spawnPoints.Length - 1);
        Transform spawnPoint = _spawnPoints[spawnIndex];

        client.PlayerObject.transform.position = spawnPoint.position;
        client.PlayerObject.transform.rotation = spawnPoint.rotation;

        Debug.Log($"Client {clientId} spawned at {spawnPoint.name}");
    }
}