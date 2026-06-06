using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Player Prefabs")]
    [SerializeField] private GameObject malePlayerPrefab;
    [SerializeField] private GameObject femalePlayerPrefab;

    [Header("Spawn Points")]
    [Tooltip("Index 0 = Host spawn, Index 1 = Client spawn")]
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    private void OnSceneLoaded(
        string sceneName,
        LoadSceneMode mode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        Debug.Log($"Scene loaded. Clients completed: {clientsCompleted.Count}");
        StartCoroutine(SpawnAllPlayers(clientsCompleted));
    }

    // Added: coroutine so each client gets a frame to fully
    // register before we attempt to spawn their player object
    private IEnumerator SpawnAllPlayers(List<ulong> clients)
    {
        yield return null;

        foreach (ulong clientId in clients)
        {
            int spawnIndex = clientId == NetworkManager.ServerClientId ? 0 : 1;
            Debug.Log($"Spawning client {clientId} at spawn point {spawnIndex}");
            yield return StartCoroutine(SpawnPlayer(clientId, spawnIndex));
        }
    }

    // Changed: SpawnPlayer is now a coroutine so it can wait
    // for despawn to fully complete before spawning the new object
    private IEnumerator SpawnPlayer(ulong clientId, int spawnIndex)
    {
        if (spawnIndex >= spawnPoints.Length)
        {
            Debug.LogWarning($"Spawn index {spawnIndex} out of range — clamping to 0");
            spawnIndex = 0;
        }

        // Despawn existing player and wait for it to complete
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn(true);

                // Wait until the old player object is fully gone
                // before attempting to spawn the new one
                float timeout = 2f;
                float elapsed = 0f;
                while (client.PlayerObject != null && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                Debug.Log($"Old player despawned for client {clientId}");
            }
        }

        // Extra safety frame after despawn
        yield return null;

        Vector3 spawnPosition = spawnPoints[spawnIndex].position;
        Quaternion spawnRotation = spawnPoints[spawnIndex].rotation;

        GameObject prefabToSpawn = clientId == NetworkManager.ServerClientId
            ? malePlayerPrefab
            : femalePlayerPrefab;

        GameObject player = Instantiate(prefabToSpawn, spawnPosition, spawnRotation);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, true);

        Debug.Log($"Spawned {prefabToSpawn.name} for client {clientId} at {spawnPoints[spawnIndex].name}");
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
    }
}