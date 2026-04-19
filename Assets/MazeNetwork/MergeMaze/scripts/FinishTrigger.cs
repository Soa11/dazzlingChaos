using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("[FinishTrigger] Hit by: " + other.name);

        var finishHandler = other.GetComponentInParent<PlayerFinishHandler>();
        if (finishHandler == null)
        {
            Debug.Log("[FinishTrigger] No PlayerFinishHandler found in parent.");
            return;
        }

        Debug.Log("[FinishTrigger] Found PlayerFinishHandler on: " + finishHandler.name);

        if (!finishHandler.IsServer)
        {
            Debug.Log("[FinishTrigger] Found handler, but this is not server.");
            return;
        }

        Debug.Log("[FinishTrigger] Calling HandleFinish()");
        finishHandler.HandleFinish();
    }
}