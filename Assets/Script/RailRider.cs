using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(Rigidbody))]
public class RailRider : MonoBehaviour
{
    // ---------- Types ----------
    [System.Serializable]
    public struct RailRef
    {
        public SplineContainer container;
        [Tooltip("For your setup this should be 0 for every container.")]
        public int splineIndex;
    }

    // ---------- Inspector ----------
    [Header("Rails (order = traversal order)")]
    [Tooltip("Add L01_S01, L01_S02, ... Each is its own SplineContainer. Spline Index = 0.")]
    public List<RailRef> rails = new List<RailRef>();

    [Tooltip("Wrap on the current spline; if OFF, we attempt to snap to the next rail when we reach an end.")]
    public bool loopOnCurrent = false;

    [Tooltip("Meters within which we auto-hop to another rail endpoint when reaching an end (used when Loop On Current = OFF).")]
    public float endSnapDistance = 1.5f;

    [Header("Speed & Input")]
    public string moveAxis = "Vertical"; // Old Input System axis
    public float maxSpeed = 20f;
    public float accel = 14f;
    public float brake = 18f;

    [Header("Slope Feel")]
    [Tooltip("Projects gravity along the rail tangent.")]
    public float gravityAlongRail = 1.0f;

    [Header("Camera/API Up Vector")]
    [Tooltip("Only used for LookRotation up; does not affect movement.")]
    public Vector3 upHint = Vector3.up;

    [Header("Debug / Convenience")]
    public bool autoDrive = true;     // move even if input axis is missing
    public float autoDriveInput = 1f; // 1 forward, -1 backward
    public bool legacyKeyFallback = true; // WASD/Arrows if axis is missing

    // ---------- Runtime ----------
    Rigidbody rb;
    int currentRail = 0;     // index into rails list
    float t = 0f;            // normalized distance along current spline
    float v = 0f;            // signed speed along rail (m/s)
    float splineLength = 1f; // cached for current rail
    float lastT = 0f;        // for visual roll (optional)

    const float kEps = 1e-5f;

    // ---------- Public API for camera ----------
    public float NormalizedT => t;
    public int CurrentRailIndex => currentRail;
    public RailRef CurrentRail => (rails != null && rails.Count > 0) ? rails[Mathf.Clamp(currentRail, 0, rails.Count - 1)] : default;

    // ---------- Lifecycle ----------
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // rail-driven
        rb.useGravity = false;

        if (!ValidateCurrent(out var s, out var world))
        {
            Debug.LogError("RailRider: assign valid rails. Each container should have ONE spline; set Spline Index = 0.");
            enabled = false; return;
        }

        splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(s, world));
        t = 1e-4f; // nudge off endpoint
        lastT = t;
    }

    void FixedUpdate()
    {
        if (!ValidateCurrent(out var spline, out var world)) return;

        // --- input → target speed
        float input = ReadMoveInput();
        float target = input * maxSpeed;
        float a = (Mathf.Abs(target) > Mathf.Abs(v)) ? accel : brake;
        v = Mathf.MoveTowards(v, target, a * Time.fixedDeltaTime);

        // --- local tangent + gravity along rail
        Vector3 tanLocal = ToV3(SplineUtility.EvaluateTangent(spline, t));
        Vector3 tanWorldN = NormalizeSafe(CurrentContainer().transform.TransformDirection(tanLocal), Vector3.forward);
        float gAlong = Vector3.Dot(Physics.gravity, tanWorldN);
        v += gAlong * gravityAlongRail * Time.fixedDeltaTime;

        // --- advance t
        float deltaNorm = (splineLength > 1e-6f) ? (v * Time.fixedDeltaTime) / splineLength : 0f;
        float tNext = t + deltaNorm;
        bool leaving = !loopOnCurrent && (tNext > 1f || tNext < 0f);
        t = loopOnCurrent ? Mathf.Repeat(tNext, 1f) : Mathf.Clamp01(tNext);

        // --- evaluate pose (LOCAL → WORLD)
        float3 pL = SplineUtility.EvaluatePosition(spline, t);
        Vector3 posW = CurrentContainer().transform.TransformPoint(ToV3(pL));

        Vector3 tanLocal2 = ToV3(SplineUtility.EvaluateTangent(spline, t));
        Vector3 tanW = NormalizeSafe(CurrentContainer().transform.TransformDirection(tanLocal2), transform.forward);

        // NaN guard
        if (!IsFinite(posW) || !IsFinite(tanW))
        {
            Debug.LogError($"[RailRider] Non-finite pose on '{CurrentContainer().name}' (rail {currentRail}) at t={t:F6}. " +
                           $"Check: Spline Index=0, rail length>0, no duplicate knots.");
            t = Mathf.Clamp01(1e-4f);
            v = 0f;
            return;
        }

        // --- end hop
        if (leaving)
        {
            if (TrySnapToNearbyEndpoint(posW, out int nextRail, out float nextT))
            {
                currentRail = nextRail;
                t = Mathf.Clamp01(nextT);

                if (ValidateCurrent(out var s2, out var w2))
                {
                    splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(s2, w2));
                    // refresh pose on new rail
                    float3 pL2 = SplineUtility.EvaluatePosition(s2, t);
                    posW = CurrentContainer().transform.TransformPoint(ToV3(pL2));

                    Vector3 tL2 = ToV3(SplineUtility.EvaluateTangent(s2, t));
                    tanW = NormalizeSafe(CurrentContainer().transform.TransformDirection(tL2), tanW);
                }
                else { v = 0f; return; }
            }
            else
            {
                // clamp and stop at the end
                t = Mathf.Clamp01(tNext);
                v = 0f;
            }
        }

        // --- move & rotate (centerline riding)
        rb.MovePosition(posW);
        Vector3 up = upHint.sqrMagnitude > kEps ? upHint.normalized : Vector3.up;
        rb.MoveRotation(Quaternion.LookRotation(tanW, up));

        lastT = t;
    }

    // ---------- Helpers ----------
    Transform CurrentContainer() => rails[currentRail].container.transform;

    static Vector3 ToV3(float3 f) => new Vector3(f.x, f.y, f.z);

    static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

    static Vector3 NormalizeSafe(Vector3 v, Vector3 fallback)
    {
        float m2 = v.sqrMagnitude;
        return (m2 > kEps * kEps) ? v / Mathf.Sqrt(m2) : fallback;
    }

    float ReadMoveInput()
    {
        float axis = 0f;
        // Old Input System axis (if project set to Both or Old)
        try { axis = Input.GetAxisRaw(moveAxis); } catch { axis = 0f; }

        // Simple key fallback
        if (legacyKeyFallback && Mathf.Approximately(axis, 0f))
        {
            bool f = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool b = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            axis = (f == b) ? 0f : (f ? 1f : -1f);
        }

        // Auto-drive for testing
        if (autoDrive && Mathf.Approximately(axis, 0f))
            axis = Mathf.Clamp(autoDriveInput, -1f, 1f);

        return axis;
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
        if (spline == null) return false;

        world = r.container.transform.localToWorldMatrix;

        // NEW: count knots + compute both lengths
        int knotCount = spline.Count;
        float lenLocal = 0f, lenWorld = 0f;
        try { lenLocal = SplineUtility.CalculateLength(spline, Matrix4x4.identity); } catch { }
        try { lenWorld = SplineUtility.CalculateLength(spline, world); } catch { }

        if (lenWorld <= 1e-4f)
        {
            Debug.LogError($"[RailRider] '{r.container.name}' (spline {sIdx}) " +
                           $"knots={knotCount}, lenLocal={lenLocal:F6}, lenWorld={lenWorld:F6}. " +
                           $"If knots<2 → data empty; if lenLocal>0 but lenWorld≈0 → parent scale.");
            return false;
        }

        splineLength = Mathf.Max(0.001f, lenWorld);

        if (!float.IsFinite(t)) t = 0f;
        t = Mathf.Clamp01(t);
        return true;
    }


    bool TrySnapToNearbyEndpoint(Vector3 fromWorldPos, out int nextRailIdx, out float nextT)
    {
        nextRailIdx = -1; nextT = 0f;
        float best = float.MaxValue;

        for (int i = 0; i < rails.Count; i++)
        {
            var r = rails[i];
            if (r.container == null || r.container.Splines == null || r.container.Splines.Count == 0) continue;

            int sIdx = Mathf.Clamp(r.splineIndex, 0, r.container.Splines.Count - 1);
            var s = r.container.Splines[sIdx];

            // check t=0 and t=1
            for (int k = 0; k < 2; k++)
            {
                float te = (k == 0) ? 0f : 1f;
                float3 pL = SplineUtility.EvaluatePosition(s, te);
                Vector3 pW = r.container.transform.TransformPoint(ToV3(pL));
                float d = Vector3.Distance(fromWorldPos, pW);
                if (float.IsFinite(d) && d <= endSnapDistance && d < best)
                {
                    best = d; nextRailIdx = i; nextT = te;
                }
            }
        }
        return nextRailIdx >= 0;
    }

    // ---------- Utilities for you ----------
    [ContextMenu("Snap To Current Rail Start")]
    void SnapToCurrentRailStart()
    {
        if (!ValidateCurrent(out var s, out _)) return;

        t = 1e-4f;
        float3 pL = SplineUtility.EvaluatePosition(s, t);
        float3 tL = SplineUtility.EvaluateTangent(s, t);

        Vector3 pW = CurrentContainer().TransformPoint(ToV3(pL));
        Vector3 tanW = NormalizeSafe(CurrentContainer().TransformDirection(ToV3(tL)), Vector3.forward);

        rb.isKinematic = true; rb.useGravity = false;
        rb.MovePosition(pW);
        rb.MoveRotation(Quaternion.LookRotation(tanW, Vector3.up));
        v = 0f;
    }

    [ContextMenu("Validate Rails")]
    void ValidateRails()
    {
        if (rails == null || rails.Count == 0) { Debug.LogError("No rails assigned."); return; }

        for (int i = 0; i < rails.Count; i++)
        {
            var r = rails[i];
            if (!r.container) { Debug.LogError($"[Rail {i}] Missing container."); continue; }
            if (r.container.Splines == null || r.container.Splines.Count == 0)
            { Debug.LogError($"[Rail {i}] '{r.container.name}' has no splines."); continue; }

            int sIdx = Mathf.Clamp(r.splineIndex, 0, r.container.Splines.Count - 1);
            var s = r.container.Splines[sIdx];

            float len = 0f;
            try { len = SplineUtility.CalculateLength(s, r.container.transform.localToWorldMatrix); }
            catch { Debug.LogError($"[Rail {i}] '{r.container.name}' length calc threw."); continue; }

            if (!(len > 1e-4f))
            { Debug.LogError($"[Rail {i}] '{r.container.name}' length≈0 (duplicate knots or collapsed segment)."); continue; }

            bool ok = true;
            for (int k = 0; k <= 10; k++)
            {
                float tt = k / 10f;
                var pL = SplineUtility.EvaluatePosition(s, tt);
                Vector3 pW = r.container.transform.TransformPoint(ToV3(pL));
                if (!IsFinite(pW)) { Debug.LogError($"[Rail {i}] non-finite at t={tt:F2}"); ok = false; break; }
            }
            if (ok) Debug.Log($"[Rail {i}] '{r.container.name}' OK (len={len:F2})");
        }
    }
}
