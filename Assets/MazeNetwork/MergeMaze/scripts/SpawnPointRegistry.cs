using System.Collections.Generic;
using UnityEngine;

public class SpawnPointRegistry : MonoBehaviour
{
    private static readonly List<Transform> cachedSpawnPoints = new List<Transform>();
    private static bool initialized = false;

    private void Awake()
    {
        RebuildCache();
    }

    private void OnEnable()
    {
        RebuildCache();
    }

    public void RebuildCache()
    {
        cachedSpawnPoints.Clear();

        foreach (Transform child in transform)
        {
            if (child != null)
            {
                cachedSpawnPoints.Add(child);
            }
        }

        initialized = true;
        Debug.Log($"[SpawnPointRegistry] Registered {cachedSpawnPoints.Count} spawn points.");
    }

    public static Transform GetRandomSpawnPoint()
    {
        if (!initialized || cachedSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[SpawnPointRegistry] No spawn points registered.");
            return null;
        }

        int index = Random.Range(0, cachedSpawnPoints.Count);
        return cachedSpawnPoints[index];
    }
}