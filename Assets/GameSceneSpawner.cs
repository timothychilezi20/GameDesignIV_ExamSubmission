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
        if (!IsServer) return;

        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Transform spawnPoint =
                clientId == NetworkManager.ServerClientId
                ? hostSpawn
                : clientSpawn;

            if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
            {
                NetworkManager.Singleton.ConnectedClients[clientId]
                    .PlayerObject.Despawn(true);
            }

            GameObject player = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            NetworkObject netObj =
                player.GetComponent<NetworkObject>();

            netObj.SpawnAsPlayerObject(clientId, true);

            Debug.Log($"Spawned player for client: {clientId}");
        }
    }
}