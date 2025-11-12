using UnityEngine;
using System.Diagnostics;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyGuard : MonoBehaviour
{
    Rigidbody rb;
    bool lastKin, lastGrav;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastKin = rb.isKinematic;
        lastGrav = rb.useGravity;
        UnityEngine.Debug.Log($"[RBGuard] Start: kin={lastKin} grav={lastGrav}");
    }

    void LateUpdate() // after most scripts
    {
        if (rb.isKinematic != lastKin)
        {
            UnityEngine.Debug.LogError($"[RBGuard] isKinematic {lastKin} -> {rb.isKinematic}\n{new StackTrace(true)}");
            lastKin = rb.isKinematic;
        }
        if (rb.useGravity != lastGrav)
        {
            UnityEngine.Debug.LogError($"[RBGuard] useGravity {lastGrav} -> {rb.useGravity}\n{new StackTrace(true)}");
            lastGrav = rb.useGravity;
        }
    }
}
