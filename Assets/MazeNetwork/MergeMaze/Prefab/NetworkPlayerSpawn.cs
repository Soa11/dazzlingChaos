using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkPlayerSpawn : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public string spawnRootName = "SpawnPoint";
    public bool useRoundRobin = true;

    private Rigidbody rb;
    private NetworkMobilePlayerController playerController;
    private bool spawnApplied = false;

    // Static persists across all instances of this script on the Server
    private static int _nextSpawnIndex = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<NetworkMobilePlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the Server/Host decides where a player starts
        if (IsServer)
        {
            Vector3 chosenSpawn = GetServerChosenSpawnPosition();

            // 1. Position the server's version of this player
            ApplySpawn(chosenSpawn);

            // 2. Tell the client who owns this object to move to this specific coordinate
            // We pass the ClientId to ensure we're talking to the right person
            SyncSpawnClientRpc(chosenSpawn, OwnerClientId);
        }
    }

    [ClientRpc]
    private void SyncSpawnClientRpc(Vector3 spawnPosition, ulong targetClientId)
    {
        // If we are the server, we already did this in OnNetworkSpawn
        if (IsServer) return;

        // Ensure only the owner of THIS player object moves themselves
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            Debug.Log($"[NetworkPlayerSpawn] Client received spawn position: {spawnPosition}");
            ApplySpawn(spawnPosition);
        }
    }

    private void ApplySpawn(Vector3 spawnWorldPosition)
    {
        // Move the transform
        transform.position = spawnWorldPosition;

        // Move the physics body
        if (rb != null)
        {
            rb.position = spawnWorldPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Snap to the nearest rail immediately so the player is "locked"
        if (playerController != null)
        {
            playerController.SetSpawnOnNearestRail(spawnWorldPosition);
        }

        spawnApplied = true;
    }

    private Vector3 GetServerChosenSpawnPosition()
    {
        GameObject root = GameObject.Find(spawnRootName);
        if (root == null)
        {
            Debug.LogWarning($"[NetworkPlayerSpawn] Could not find spawn root '{spawnRootName}'. Spawning at origin.");
            return Vector3.zero;
        }

        List<Transform> points = new List<Transform>();
        foreach (Transform child in root.transform)
        {
            points.Add(child);
        }

        if (points.Count == 0) return transform.position;

        int index;
        if (useRoundRobin)
        {
            // Use the static counter to ensure Player 1 gets index 0, Player 2 gets index 1, etc.
            index = _nextSpawnIndex % points.Count;
            _nextSpawnIndex++;
        }
        else
        {
            index = UnityEngine.Random.Range(0, points.Count);
        }

        Transform chosen = points[index];
        Debug.Log($"[NetworkPlayerSpawn] Server assigned index {index} ({chosen.name}) to Player {OwnerClientId}");
        return chosen.position;
    }
}