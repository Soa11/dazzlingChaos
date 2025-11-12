using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GravitySanity : MonoBehaviour
{
    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }
}
