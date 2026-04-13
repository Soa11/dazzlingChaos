using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerProximityVibrate : NetworkBehaviour
{
    [Header("Proximity Settings")]
    public float sensingRadius = 8f;
    public float checkInterval = 0.2f;
    public float vibrationCooldown = 1.5f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private float nextVibrationTime = 0f;
    private bool wasNearSomeone = false;
    private Coroutine proximityCoroutine;

    private static readonly List<PlayerProximityVibrate> AllPlayers = new List<PlayerProximityVibrate>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!AllPlayers.Contains(this))
            AllPlayers.Add(this);

        if (enableDebugLogs)
        {
            Debug.Log($"[Proximity] OnNetworkSpawn | {gameObject.name} | IsOwner={IsOwner} | IsSpawned={IsSpawned}");
        }

        if (IsOwner)
        {
            proximityCoroutine = StartCoroutine(ProximityCheckLoop());

            if (enableDebugLogs)
                Debug.Log($"[Proximity] Started on owner: {gameObject.name}");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (proximityCoroutine != null)
        {
            StopCoroutine(proximityCoroutine);
            proximityCoroutine = null;
        }

        AllPlayers.Remove(this);

        base.OnNetworkDespawn();
    }

    private IEnumerator ProximityCheckLoop()
    {
        yield return new WaitForSeconds(1f);

        while (IsSpawned && IsOwner)
        {
            bool isNearSomeone = IsNearAnyOtherPlayer();

            if (enableDebugLogs)
                Debug.Log($"[Proximity] {gameObject.name} | Near someone: {isNearSomeone}");

            if (isNearSomeone && !wasNearSomeone && Time.time >= nextVibrationTime)
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
                nextVibrationTime = Time.time + vibrationCooldown;

                if (enableDebugLogs)
                    Debug.Log($"[Proximity] {gameObject.name} | Vibrate triggered");
            }

            wasNearSomeone = isNearSomeone;
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private bool IsNearAnyOtherPlayer()
    {
        Vector3 myPosition = transform.position;

        foreach (var other in AllPlayers)
        {
            if (other == null) continue;
            if (other == this) continue;
            if (!other.IsSpawned) continue;

            float distance = Vector3.Distance(myPosition, other.transform.position);

            if (enableDebugLogs)
                Debug.Log($"[Proximity] Checking {gameObject.name} -> {other.gameObject.name} | Distance={distance:F2}");

            if (distance <= sensingRadius)
                return true;
        }

        return false;
    }
}