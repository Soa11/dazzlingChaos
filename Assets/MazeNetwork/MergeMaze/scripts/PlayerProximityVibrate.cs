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
    public bool enableEnterExitLogs = true;

    private float nextVibrationTime = 0f;
    private bool wasNearSomeone = false;
    private Coroutine proximityCoroutine;

    private static readonly List<PlayerProximityVibrate> AllPlayers = new List<PlayerProximityVibrate>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!AllPlayers.Contains(this))
            AllPlayers.Add(this);

        if (IsOwner)
            proximityCoroutine = StartCoroutine(ProximityCheckLoop());
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

            if (isNearSomeone && !wasNearSomeone)
            {
#if UNITY_ANDROID || UNITY_IOS
                if (Time.time >= nextVibrationTime)
                {
                    Handheld.Vibrate();
                    nextVibrationTime = Time.time + vibrationCooldown;
                }
#endif

                if (enableEnterExitLogs)
                    Debug.Log($"[Proximity] ENTER | {gameObject.name}");
            }
            else if (!isNearSomeone && wasNearSomeone)
            {
                if (enableEnterExitLogs)
                    Debug.Log($"[Proximity] EXIT | {gameObject.name}");
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

            if (distance <= sensingRadius)
                return true;
        }

        return false;
    }
}