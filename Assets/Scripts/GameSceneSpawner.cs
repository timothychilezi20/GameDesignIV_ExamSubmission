using UnityEngine;

public class GameSceneSpawner : Unity.Netcode.NetworkBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        foreach (ulong clientId in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds)
        {
            Transform spawnPoint =
                clientId == Unity.Netcode.NetworkManager.ServerClientId
                ? hostSpawn
                : clientSpawn;

            if (spawnPoint == null)
            {
                Debug.LogError($"Spawn point is null for ClientId {clientId}");
                continue;
            }

            GameObject player = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            Unity.Netcode.NetworkObject netObj = player.GetComponent<Unity.Netcode.NetworkObject>();

            if (netObj == null)
            {
                Debug.LogError("Player prefab is missing a NetworkObject component!");
                continue;
            }

            netObj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"Spawned player for ClientId {clientId} at {spawnPoint.name}");
        }
    }
}