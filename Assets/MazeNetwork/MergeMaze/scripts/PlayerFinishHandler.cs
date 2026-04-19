using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerFinishHandler : NetworkBehaviour
{
    private bool isFinished = false;

    public void HandleFinish()
    {
        Debug.Log("[PlayerFinishHandler] HandleFinish called on " + name);

        if (!IsServer)
        {
            Debug.Log("[PlayerFinishHandler] Not server, stopping.");
            return;
        }

        if (isFinished)
        {
            Debug.Log("[PlayerFinishHandler] Already finished, stopping.");
            return;
        }

        isFinished = true;

        DisableMovement();
        DisableCollisions();

        Debug.Log("[PlayerFinishHandler] Starting despawn coroutine.");
        StartCoroutine(DespawnAfterDelay(2f));
    }

    private void DisableMovement()
    {
        var move = GetComponent<NetworkMobilePlayerController>();
        if (move != null)
        {
            move.enabled = false;
            Debug.Log("[PlayerFinishHandler] Movement disabled.");
        }
        else
        {
            Debug.Log("[PlayerFinishHandler] NetworkMobilePlayerController not found.");
        }
    }

    private void DisableCollisions()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        Debug.Log("[PlayerFinishHandler] Collisions disabled. Count = " + colliders.Length);
    }

    private IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log("[PlayerFinishHandler] DespawnAfterDelay reached.");

        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.enabled = false;
            Debug.Log("[PlayerFinishHandler] NetworkTransform disabled.");
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log("[PlayerFinishHandler] Despawning NetworkObject.");
            NetworkObject.Despawn();
        }
        else
        {
            Debug.Log("[PlayerFinishHandler] NetworkObject missing or not spawned.");
        }
    }
}