using UnityEngine;
using Unity.Netcode;

public class GameSceneSpawner : NetworkBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    public override void OnNetworkSpawn()
    {
        // Only server spawns players
        if (!IsServer) return;

        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Transform spawnPoint =
                clientId == 0
                ? hostSpawn
                : clientSpawn;

            GameObject player = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            player.GetComponent<NetworkObject>()
                .SpawnAsPlayerObject(clientId, true);

            Debug.Log("Spawned player for ClientId: " + clientId);
        }
    }
}