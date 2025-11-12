using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(Rigidbody))]
public class SplineRailMotorMultiMinimal : MonoBehaviour
{
    [System.Serializable]
    public struct RailRef
    {
        public SplineContainer container;
        public int splineIndex;   // for you: 0 for all L01_S0X
    }

    [Header("Rails")]
    public List<RailRef> rails = new List<RailRef>();
    public int currentRail = 0;          // index into rails list
    public bool loop = true;             // wrap t on current spline
    public float endSnapDistance = 1.25f; // meters to snap to next rail endpoint

    [Header("Speed & Input")]
    public float maxSpeed = 22f;
    public float accel = 14f;
    public float brake = 18f;
    public string moveAxis = "Vertical";

    [Header("Gravity on slopes")]
    public float gravityAlongRail = 1.0f;

    [Header("Orientation")]
    public bool alignToTangent = true;
    public Vector3 upHint = Vector3.up;

    [Header("Rolling Visual (optional)")]
    public Transform rollingVisual;
    public float visualRadius = 0.5f;

    Rigidbody rb;
    float t, splineLength, v, lastT;

    static Vector3 V3(float3 f) => new Vector3(f.x, f.y, f.z);
    const float kEps = 1e-5f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (!ValidateCurrent(out var spline, out var world))
        {
            Debug.LogError("Assign at least one valid SplineContainer.");
            enabled = false; return;
        }

        splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(spline, world));
        t = Mathf.Repeat(t, 1f);
        lastT = t;
    }

    void FixedUpdate()
    {
        if (!ValidateCurrent(out var spline, out var world)) return;

        // input → target speed (same as your original)
        float input = 0f;
        try { input = Input.GetAxisRaw(moveAxis); } catch { input = 0f; }
        float target = input * maxSpeed;
        float a = (Mathf.Abs(target) > Mathf.Abs(v)) ? accel : brake;
        v = Mathf.MoveTowards(v, target, a * Time.fixedDeltaTime);

        // tangent (LOCAL), then world for gravity projection
        float3 tanLocal = SplineUtility.EvaluateTangent(spline, t);
        float3 tanLocalN = math.lengthsq(tanLocal) > 1e-8f ? math.normalize(tanLocal) : new float3(0, 0, 1);
        Vector3 tanWorld = rails[currentRail].container.transform.TransformDirection(V3(tanLocalN));
        float gAlong = Vector3.Dot(Physics.gravity, tanWorld);
        v += gAlong * gravityAlongRail * Time.fixedDeltaTime;

        // advance t
        float deltaNorm = (v * Time.fixedDeltaTime) / splineLength;
        float tNext = t + deltaNorm;

        bool leaving = !loop && (tNext > 1f || tNext < 0f);
        t = loop ? Mathf.Repeat(tNext, 1f) : Mathf.Clamp01(tNext);

        // evaluate pose (LOCAL → WORLD)
        float3 posLocal = SplineUtility.EvaluatePosition(spline, t);
        float3 tanLocal2 = SplineUtility.EvaluateTangent(spline, t);
        float3 tanLocalN2 = math.lengthsq(tanLocal2) > 1e-8f ? math.normalize(tanLocal2) : new float3(0, 0, 1);

        Vector3 posWorld = rails[currentRail].container.transform.TransformPoint(V3(posLocal));
        Vector3 tanWorld2 = rails[currentRail].container.transform.TransformDirection(V3(tanLocalN2));
        Vector3 up = upHint.sqrMagnitude > kEps ? upHint.normalized : Vector3.up;

        // handle end → try snap to another rail, else stop
        if (leaving)
        {
            if (TrySnapToNextRail(posWorld, out int nextIndex, out float nextT))
            {
                currentRail = nextIndex;
                t = nextT;
                if (ValidateCurrent(out var s2, out var w2))
                    splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(s2, w2));

                // re-evaluate pose on new rail
                SplineUtility.Evaluate(s2, t, out float3 pL, out float3 tL, out _);
                posWorld = rails[currentRail].container.transform.TransformPoint(V3(pL));
                tanWorld2 = rails[currentRail].container.transform.TransformDirection(V3(math.normalize(tL)));
            }
            else
            {
                // clamp and stop (keeps old behaviour spirit; no fall yet)
                t = Mathf.Clamp01(tNext); // already clamped
                v = 0f;
            }
        }

        // move & rotate (centerline, like your original)
        rb.MovePosition(posWorld);
        if (alignToTangent)
        {
            Vector3 fwd = tanWorld2.sqrMagnitude > kEps ? tanWorld2 : transform.forward;
            rb.MoveRotation(Quaternion.LookRotation(fwd, up));
        }

        // rolling visual
        if (rollingVisual && visualRadius > 0f)
        {
            float dtNorm = Mathf.DeltaAngle(lastT * 360f, t * 360f) / 360f;
            float ds = Mathf.Sign(v) * Mathf.Abs(dtNorm) * splineLength;
            float angleDeg = (ds / (visualRadius * 2f * Mathf.PI)) * 360f;
            rollingVisual.Rotate(Vector3.right, angleDeg, Space.Self);
            lastT = t;
        }
    }

    bool ValidateCurrent(out Spline spline, out Matrix4x4 world)
    {
        spline = null; world = Matrix4x4.identity;
        if (rails == null || rails.Count == 0) return false;

        currentRail = Mathf.Clamp(currentRail, 0, rails.Count - 1);
        var r = rails[currentRail];
        if (r.container == null || r.container.Splines == null || r.container.Splines.Count == 0) return false;

        int sIdx = Mathf.Clamp(r.splineIndex, 0, r.container.Splines.Count - 1);
        spline = r.container.Splines[sIdx];
        world = r.container.transform.localToWorldMatrix;
        return spline != null;
    }

    bool TrySnapToNextRail(Vector3 fromWorldPos, out int nextRailIdx, out float nextT)
    {
        nextRailIdx = -1; nextT = 0f;
        float best = float.MaxValue;

        for (int i = 0; i < rails.Count; i++)
        {
            var r = rails[i];
            if (r.container == null || r.container.Splines.Count == 0) continue;
            var s = r.container.Splines[Mathf.Clamp(r.splineIndex, 0, r.container.Splines.Count - 1)];

            // check both ends
            for (int k = 0; k < 2; k++)
            {
                float te = (k == 0) ? 0f : 1f;
                SplineUtility.Evaluate(s, te, out float3 pL, out _, out _);
                Vector3 pW = r.container.transform.TransformPoint(V3(pL));
                float d = Vector3.Distance(fromWorldPos, pW);
                if (d <= endSnapDistance && d < best)
                {
                    best = d; nextRailIdx = i; nextT = te;
                }
            }
        }
        return nextRailIdx >= 0;
    }

    // --- Tiny API for the camera (no reflection) ---
    public bool GetRailPose(out Vector3 posW, out Vector3 tanW, out Vector3 upW)
    {
        posW = tanW = upW = Vector3.zero;
        if (!ValidateCurrent(out var s, out _)) return false;

        SplineUtility.Evaluate(s, t, out float3 pL, out float3 tL, out _);
        var r = rails[currentRail];
        posW = r.container.transform.TransformPoint(V3(pL));
        tanW = r.container.transform.TransformDirection(V3(math.normalize(tL)));
        upW = (upHint.sqrMagnitude > kEps) ? upHint.normalized : Vector3.up;
        return true;
    }

    public float NormalizedT => t;
    public int CurrentRailIndex => currentRail;
    public RailRef CurrentRail => rails != null && rails.Count > 0 ? rails[Mathf.Clamp(currentRail, 0, rails.Count - 1)] : default;
}
