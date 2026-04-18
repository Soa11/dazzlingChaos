using UnityEngine;

public class RailDebugWatcher : MonoBehaviour
{
    public NetworkMobilePlayerController targetPlayer;

    float timer = 0f;

    void Update()
    {
        if (targetPlayer == null)
            return;

        timer += Time.deltaTime;

        // log every 1 second (not every frame)
        if (timer > 1f)
        {
            timer = 0f;

            Debug.Log(
                $"[RailDebug] Rail: {targetPlayer.CurrentRailName} | " +
                $"Index: {targetPlayer.CurrentRailIndex} | " +
                $"T: {targetPlayer.T:F3} | " +
                $"Locked: {targetPlayer.IsLocked}"
            );
        }
    }
}