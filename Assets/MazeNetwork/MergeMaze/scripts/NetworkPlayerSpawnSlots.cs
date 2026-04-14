using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkPlayerSpawnSlots : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public string spawnRootName = "SpawnPoint";

    [Tooltip("Use _1 slots first, then _2 slots.")]
    public bool prioritizePrimarySlots = true;

    private Rigidbody rb;
    private NetworkMobilePlayerController playerController;

    private static int _nextSpawnIndex = 0;
    private static bool _sessionInitialized = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<NetworkMobilePlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        InitializeSessionIfNeeded();

        Transform chosenSpawn = GetServerChosenSpawnTransform();

        if (chosenSpawn == null)
        {
            Debug.LogWarning("[NetworkPlayerSpawnSlots] No valid spawn point found. Using current transform.");
            return;
        }

        ApplySpawn(chosenSpawn.position, chosenSpawn.rotation);
        SyncSpawnClientRpc(chosenSpawn.position, chosenSpawn.rotation, OwnerClientId);
    }

    private void InitializeSessionIfNeeded()
    {
        if (_sessionInitialized) return;

        _nextSpawnIndex = 0;
        _sessionInitialized = true;

        Debug.Log("[NetworkPlayerSpawnSlots] New host session initialized. Spawn index reset to 0.");
    }

    [ClientRpc]
    private void SyncSpawnClientRpc(Vector3 spawnPosition, Quaternion spawnRotation, ulong targetClientId)
    {
        if (IsServer) return;
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        Debug.Log($"[NetworkPlayerSpawnSlots] Client received spawn: {spawnPosition} / {spawnRotation.eulerAngles}");
        ApplySpawn(spawnPosition, spawnRotation);
    }

    private void ApplySpawn(Vector3 spawnWorldPosition, Quaternion spawnWorldRotation)
    {
        transform.SetPositionAndRotation(spawnWorldPosition, spawnWorldRotation);

        if (rb != null)
        {
            rb.position = spawnWorldPosition;
            rb.rotation = spawnWorldRotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        if (playerController != null)
        {
            playerController.SetSpawnOnNearestRail(spawnWorldPosition);
        }
    }

    private Transform GetServerChosenSpawnTransform()
    {
        GameObject root = GameObject.Find(spawnRootName);
        if (root == null)
        {
            Debug.LogWarning($"[NetworkPlayerSpawnSlots] Could not find spawn root '{spawnRootName}'.");
            return null;
        }

        List<Transform> allPoints = new List<Transform>();
        foreach (Transform child in root.transform)
        {
            if (child != null && child.gameObject.activeInHierarchy)
                allPoints.Add(child);
        }

        if (allPoints.Count == 0)
        {
            Debug.LogWarning("[NetworkPlayerSpawnSlots] No spawn points found under root.");
            return null;
        }

        List<Transform> orderedPoints = BuildOrderedSpawnList(allPoints);

        int index = _nextSpawnIndex % orderedPoints.Count;
        Transform chosen = orderedPoints[index];
        _nextSpawnIndex++;

        Debug.Log($"[NetworkPlayerSpawnSlots] Assigned spawn index {index} ({chosen.name}) to player {OwnerClientId}");

        return chosen;
    }

    private List<Transform> BuildOrderedSpawnList(List<Transform> allPoints)
    {
        if (!prioritizePrimarySlots)
        {
            return allPoints
                .OrderBy(t => t.name, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        List<Transform> primary = new List<Transform>();
        List<Transform> secondary = new List<Transform>();
        List<Transform> other = new List<Transform>();

        foreach (Transform t in allPoints)
        {
            string lower = t.name.ToLowerInvariant();

            if (lower.EndsWith("_1"))
                primary.Add(t);
            else if (lower.EndsWith("_2"))
                secondary.Add(t);
            else
                other.Add(t);
        }

        primary = primary.OrderBy(t => t.name, System.StringComparer.OrdinalIgnoreCase).ToList();
        secondary = secondary.OrderBy(t => t.name, System.StringComparer.OrdinalIgnoreCase).ToList();
        other = other.OrderBy(t => t.name, System.StringComparer.OrdinalIgnoreCase).ToList();

        List<Transform> result = new List<Transform>();
        result.AddRange(primary);
        result.AddRange(secondary);
        result.AddRange(other);

        return result;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer && NetworkManager.Singleton != null)
        {
            if (!NetworkManager.Singleton.IsListening || NetworkManager.Singleton.ConnectedClients.Count <= 1)
            {
                _sessionInitialized = false;
            }
        }
    }
}