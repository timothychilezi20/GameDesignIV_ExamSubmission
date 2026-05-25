using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Points")]
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

        Debug.Log("Scene fully loaded. Spawning gameplay players.");

        int spawnIndex = 0;

        foreach (ulong clientId in clientsCompleted)
        {
            SpawnPlayer(clientId, spawnIndex);

            spawnIndex++;
        }
    }

    private void SpawnPlayer(ulong clientId, int spawnIndex)
    {
        // Remove OLD player if one exists
        if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
        {
            NetworkObject oldPlayer =
                NetworkManager.Singleton
                .ConnectedClients[clientId]
                .PlayerObject;

            oldPlayer.Despawn(true);
        }

        // Clamp spawn index
        if (spawnIndex >= spawnPoints.Length)
        {
            spawnIndex = 0;
        }

        Vector3 spawnPosition =
            spawnPoints[spawnIndex].position;

        Quaternion spawnRotation =
            spawnPoints[spawnIndex].rotation;

        GameObject player = Instantiate(
            playerPrefab,
            spawnPosition,
            spawnRotation
        );

        NetworkObject netObj =
            player.GetComponent<NetworkObject>();

        netObj.SpawnAsPlayerObject(clientId, true);

        Debug.Log(
            $"Spawned gameplay player for Client {clientId} at {spawnPosition}"
        );
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager != null &&
            NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }
}