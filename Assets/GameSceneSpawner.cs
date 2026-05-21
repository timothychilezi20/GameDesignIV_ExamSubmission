using UnityEngine;
using Unity.Netcode;

public class GameSceneSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;

    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Transform spawnPoint =
                clientId == 0 ? hostSpawn : clientSpawn;

            GameObject player = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            player.GetComponent<NetworkObject>()
                  .SpawnAsPlayerObject(clientId);
        }
    }
}