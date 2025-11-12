// 12/11/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(Camera))]
public class RailFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public PlayerMove player;                // Assign your player’s PlayerMove script

    [Header("Follow Behaviour")]
    public float followDistance = 4f;       // Meters behind along tangent
    public float followHeight = 1.5f;       // Meters above spline center
    public float lookAhead = 2f;            // Meters ahead for aim
    public float positionLerp = 8f;         // Lerp speed for position
    public float rotationLerp = 8f;         // Lerp speed for rotation

    [Header("Offsets & Up")]
    public Vector3 worldOffset = Vector3.zero;
    public bool useSplineUp = true;         // Otherwise uses Vector3.up

    const float kEps = 1e-6f;

    void LateUpdate()
    {
        if (!player) return;

        // Get the current rail and spline from the player
        int currentRailIndex = player.CurrentRailIndex; // Correct property name
        if (!player.ValidateRail(currentRailIndex)) return;

        var rail = player.rails[currentRailIndex];
        var container = rail.container;
        if (!container || container.Splines == null || container.Splines.Count == 0) return;

        int sIdx = Mathf.Clamp(rail.splineIndex, 0, container.Splines.Count - 1);
        Spline s = container.Splines[sIdx];

        // Current t from player
        float t = player.NormalizedT; // Correct property name

        // Evaluate pose at t (LOCAL -> WORLD)
        SplineUtility.Evaluate(s, t, out float3 pL, out float3 tL, out float3 uL);
        Vector3 posW = container.transform.TransformPoint((Vector3)pL);
        Vector3 tanW = container.transform.TransformDirection(math.normalizesafe(tL, new float3(0, 0, 1)));
        Vector3 upW = container.transform.TransformDirection(math.normalizesafe(uL, new float3(0, 1, 0)));

        // Look-ahead sampling (no need to know player.loop; just clamp)
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
        if (aimDir.sqrMagnitude < kEps) aimDir = tanW;     // Guard against zero vector
        Vector3 camUp = useSplineUp ? upW : Vector3.up;

        Quaternion targetRot = Quaternion.LookRotation(aimDir.normalized, camUp);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot,
            1f - Mathf.Exp(-rotationLerp * Time.deltaTime)
        );
    }
}