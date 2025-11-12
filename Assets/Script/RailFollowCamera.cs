using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(Camera))]
public class RailFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public RailRider rider;                   // assign your player’s RailRider

    [Header("Follow Behaviour")]
    public float followDistance = 4f;         // meters behind along tangent
    public float followHeight = 1.5f;       // meters above spline center
    public float lookAhead = 2f;         // meters ahead for aim
    public float positionLerp = 8f;
    public float rotationLerp = 8f;

    [Header("Offsets & Up")]
    public Vector3 worldOffset = Vector3.zero;
    public bool useSplineUp = true;        // otherwise uses Vector3.up

    const float kEps = 1e-6f;

    void LateUpdate()
    {
        if (!rider) return;

        // Pull the current rail from the rider (public in your script)
        var rail = rider.CurrentRail;
        var container = rail.container;
        if (!container || container.Splines == null || container.Splines.Count == 0) return;

        int sIdx = Mathf.Clamp(rail.splineIndex, 0, container.Splines.Count - 1);
        Spline s = container.Splines[sIdx];

        // Current t from rider (public NormalizedT)
        float t = rider.NormalizedT;

        // Evaluate pose at t (LOCAL -> WORLD)
        SplineUtility.Evaluate(s, t, out float3 pL, out float3 tL, out float3 uL);
        Vector3 posW = container.transform.TransformPoint((Vector3)pL);
        Vector3 tanW = container.transform.TransformDirection(math.normalizesafe(tL, new float3(0, 0, 1)));
        Vector3 upW = container.transform.TransformDirection(math.normalizesafe(uL, new float3(0, 1, 0)));

        // Look-ahead sampling (no need to know rider.loop; just clamp)
        float length = Mathf.Max(0.001f, SplineUtility.CalculateLength(s, container.transform.localToWorldMatrix));
        float dn = Mathf.Clamp01(lookAhead / length);
        float tAhead = Mathf.Clamp01(t + dn);

        float3 pLAhead = SplineUtility.EvaluatePosition(s, tAhead);
        Vector3 posAheadW = container.transform.TransformPoint((Vector3)pLAhead);

        // Desired camera position and orientation
        Vector3 desired = posW - tanW * followDistance + Vector3.up * followHeight + worldOffset;
        transform.position = Vector3.Lerp(
            transform.position, desired,
            1f - Mathf.Exp(-positionLerp * Time.deltaTime)
        );

        Vector3 aimDir = (posAheadW - transform.position);
        if (aimDir.sqrMagnitude < kEps) aimDir = tanW;     // guard zero vector
        Vector3 camUp = useSplineUp ? upW : Vector3.up;

        Quaternion targetRot = Quaternion.LookRotation(aimDir.normalized, camUp);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot,
            1f - Mathf.Exp(-rotationLerp * Time.deltaTime)
        );
    }
}
