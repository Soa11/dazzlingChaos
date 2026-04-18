using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [Header("Setup")]
    public GameObject mapMarkerPrefab;
    public Transform markerParent;

    [Header("Player Search")]
    public string playerTag = "Player";

    private readonly Dictionary<Transform, GameObject> markerByPlayer = new();

    void Update()
    {
        RefreshMarkers();
    }

    void RefreshMarkers()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

        HashSet<Transform> currentPlayers = new();

        foreach (GameObject player in players)
        {
            if (player == null) continue;

            Transform playerTransform = player.transform;
            currentPlayers.Add(playerTransform);

            if (!markerByPlayer.ContainsKey(playerTransform))
            {
                CreateMarker(playerTransform);
            }
        }

        List<Transform> toRemove = new();

        foreach (var kvp in markerByPlayer)
        {
            if (kvp.Key == null || !currentPlayers.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }

                toRemove.Add(kvp.Key);
            }
        }

        foreach (Transform deadPlayer in toRemove)
        {
            markerByPlayer.Remove(deadPlayer);
        }
    }

    void CreateMarker(Transform playerTransform)
    {
        if (mapMarkerPrefab == null) return;

        GameObject marker = Instantiate(mapMarkerPrefab, markerParent);

        MapMarkerFollower follower = marker.GetComponent<MapMarkerFollower>();
        if (follower != null)
        {
            follower.target = playerTransform;
        }

        MapNameSync nameSync = marker.GetComponent<MapNameSync>();
        if (nameSync != null)
        {
            nameSync.target = playerTransform;
        }

        markerByPlayer[playerTransform] = marker;
    }
}