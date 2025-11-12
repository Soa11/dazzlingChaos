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

    enum Mode { OnRail, InAir }
    public enum OffsetAxis { Up, Right }

    // ---------- Inspector ----------
    [Header("Rails (order = traversal order)")]
    [Tooltip("Add L01_S01, L01_S02, ... Each is its own SplineContainer. Spline Index = 0.")]
    public List<RailRef> rails = new List<RailRef>();

    [Tooltip("Wrap on the current spline; if OFF, we attempt to snap to the next rail when we reach an end.")]
    public bool loopOnCurrent = false;

    [Tooltip("Meters within which we auto-hop to another rail endpoint when reaching an end (used when Loop On Current = OFF).")]
    public float endSnapDistance = 1.5f;

    [Header("Speed & Input")]
    [Tooltip("Old Input System axis name ('Vertical' by default).")]
    public string moveAxis = "Vertical";
    public float maxSpeed = 20f;
    public float accel = 14f;
    public float brake = 18f;

    [Header("Require Input")]
    [Tooltip("When ON, you won't move unless input != 0.")]
    public bool requireInputToMove = true;

    [Header("Slope Assist (optional)")]
    [Tooltip("Projects gravity along the rail tangent (0 = off).")]
    [Range(0f, 2f)] public float gravityAlongRail = 0f;

    [Header("Camera/API Up Vector")]
    [Tooltip("Only used for LookRotation up; does not affect movement.")]
    public Vector3 upHint = Vector3.up;

    [Header("Legacy Key Fallback")]
    public bool legacyKeyFallback = true; // WASD/Arrows if axis missing

    [Header("Ride Offset")]
    [Tooltip("Tube radius + player radius. Example: 0.25 (tube) + 0.5 (sphere) = 0.75")]
    public float surfaceOffset = 0.75f;
    public OffsetAxis offsetAxis = OffsetAxis.Up;
    [Tooltip("Use the spline's up vector for banking; off = global up")]
    public bool useSplineUp = true;

    [Header("Jump")]
    public KeyCode jumpKey = KeyCode.Space;
    [Tooltip("Initial upward impulse when leaving the rail.")]
    public float jumpImpulse = 8f;
    [Tooltip("Extra impulse along the current tangent.")]
    public float jumpForwardBoost = 2f;
    [Tooltip("Time after leaving rail you can still jump (forgiving controls).")]
    public float coyoteTime = 0.12f;
    [Tooltip("Buffer a jump press slightly before landing.")]
    public float jumpBufferTime = 0.12f;

    [Header("Air Control (optional)")]
    [Tooltip("Small horizontal control while in air, along last rail tangent.")]
    public float airAccel = 10f;
    public float airMaxSpeed = 10f;

    [Header("Fail / Respawn")]
    [Tooltip("If Y falls below this, respawn.")]
    public float killY = -50f;
    [Tooltip("If you're this far from any rail for too long, fail.")]
    public float maxDetachDistance = 6f;
    public float maxAirTime = 6f;

    [Tooltip("Meters for auto re-grab when near a rail.")]
    public float regrabDistance = 0.75f;

    [Header("Regrab Control")]
    [Tooltip("Time after leaving the rail before auto regrab is allowed.")]
    public float regrabLockout = 0.3f;

    // ---------- Runtime ----------
    Rigidbody rb;
    Mode mode = Mode.OnRail;

    int currentRail = 0;
    float t = 0f;
    float v = 0f;
    float splineLength = 1f;
    float lastT = 0f;

    // timers
    float timeSinceRail = 0f;
    float timeSinceJumpPressed = 999f;
    float timeSinceLeaveRail = 999f;

    // last safe pose (for respawn)
    int safeRailIdx = 0;
    float safeT = 1e-4f;

    const float kEps = 1e-5f;

    // ---------- Public API ----------
    public float NormalizedT => t;
    public int CurrentRailIndex => currentRail;
    public RailRef CurrentRail => (rails != null && rails.Count > 0) ? rails[Mathf.Clamp(currentRail, 0, rails.Count - 1)] : default;

    // ---------- Lifecycle ----------
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;        // rail-driven by default
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (!ValidateCurrent(out var s, out var world))
        {
            Debug.LogError("RailRider: assign valid rails. Each container should have ONE spline; set Spline Index = 0.");
            enabled = false; return;
        }

        splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(s, world));
        t = 1e-4f;
        lastT = t;

        safeRailIdx = currentRail;
        safeT = t;
    }

    void Update()
    {
        if (Input.GetKeyDown(jumpKey))
            timeSinceJumpPressed = 0f;

        timeSinceJumpPressed += Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (!ValidateCurrent(out var spline, out var world)) return;

        switch (mode)
        {
            case Mode.OnRail: TickOnRail(spline, world); break;
            case Mode.InAir: TickInAir(); break;
        }
    }

    // ---------- Modes ----------
    void TickOnRail(Spline spline, Matrix4x4 world)
    {
        // --- input
        float input = ReadMoveInput();
        float desired = (requireInputToMove && Mathf.Approximately(input, 0f)) ? 0f : input * maxSpeed;

        // accel vs brake
        float a = (Mathf.Abs(desired) > Mathf.Abs(v)) ? accel : brake;
        v = Mathf.MoveTowards(v, desired, a * Time.fixedDeltaTime);

        // optional slope assist
        if (gravityAlongRail > 0f)
        {
            Vector3 tanLocal = ToV3(SplineUtility.EvaluateTangent(spline, t));
            Vector3 tanWorldN = NormalizeSafe(CurrentContainer().transform.TransformDirection(tanLocal), Vector3.forward);
            float gAlong = Vector3.Dot(Physics.gravity, tanWorldN);
            v += gAlong * gravityAlongRail * Time.fixedDeltaTime;
        }

        // advance t
        float deltaNorm = (splineLength > 1e-6f) ? (v * Time.fixedDeltaTime) / splineLength : 0f;
        float tNext = t + deltaNorm;
        bool leaving = !loopOnCurrent && (tNext > 1f || tNext < 0f);
        t = loopOnCurrent ? Mathf.Repeat(tNext, 1f) : Mathf.Clamp01(tNext);

        // pose
        float3 pL = SplineUtility.EvaluatePosition(spline, t);
        Vector3 posW = CurrentContainer().transform.TransformPoint(ToV3(pL));

        SplineUtility.Evaluate(spline, t, out _, out float3 tanL, out float3 upL);
        Vector3 tanW = NormalizeSafe(CurrentContainer().transform.TransformDirection(ToV3(tanL)), transform.forward);
        Vector3 upW = useSplineUp
            ? CurrentContainer().transform.TransformDirection((Vector3)math.normalizesafe(upL, new float3(0, 1, 0)))
            : Vector3.up;

        Vector3 rightW = Vector3.Cross(upW, tanW).normalized;
        Vector3 offsetDir = (offsetAxis == OffsetAxis.Up) ? upW : rightW;
        posW += offsetDir * surfaceOffset;

        if (!IsFinite(posW) || !IsFinite(tanW))
        {
            Debug.LogError($"[RailRider] Non-finite pose on '{CurrentContainer().name}' (rail {currentRail}) at t={t:F6}.");
            t = Mathf.Clamp01(1e-4f);
            v = 0f;
            return;
        }

        // end hop between rails
        if (leaving)
        {
            if (TrySnapToNearbyEndpoint(posW, out int nextRail, out float nextT))
            {
                currentRail = nextRail;
                t = Mathf.Clamp01(nextT);

                if (ValidateCurrent(out var s2, out var w2))
                {
                    splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(s2, w2));
                    float3 pL2 = SplineUtility.EvaluatePosition(s2, t);
                    posW = CurrentContainer().transform.TransformPoint(ToV3(pL2));
                    Vector3 tL2 = ToV3(SplineUtility.EvaluateTangent(s2, t));
                    tanW = NormalizeSafe(CurrentContainer().transform.TransformDirection(tL2), tanW);
                }
                else { v = 0f; return; }
            }
            else
            {
                t = Mathf.Clamp01(tNext);
                v = 0f;
            }
        }

        // move/rotate
        rb.MovePosition(posW);
        Vector3 up = useSplineUp ? upW : (upHint.sqrMagnitude > kEps ? upHint.normalized : Vector3.up);
        rb.MoveRotation(Quaternion.LookRotation(tanW, up));

        // store safe pose
        safeRailIdx = currentRail;
        safeT = t;

        // reset "left rail" timer while we are on-rail
        timeSinceLeaveRail = 999f;

        // jump (world-up to avoid tilted spline-up weirdness)
        bool jumpBuffered = timeSinceJumpPressed <= jumpBufferTime;
        if (jumpBuffered || (Input.GetKey(jumpKey) && timeSinceRail <= coyoteTime))
        {
            EnterAir(tanW, Vector3.up);
            timeSinceJumpPressed = 999f;
            return;
        }

        timeSinceRail = 0f;
        lastT = t;
    }

    void TickInAir()
    {
        timeSinceRail += Time.fixedDeltaTime;
        timeSinceLeaveRail += Time.fixedDeltaTime;

        // light air control along current forward
        float input = ReadMoveInput();
        if (Mathf.Abs(input) > 0f && airAccel > 0f)
        {
            Vector3 forward = transform.forward;
            Vector3 proj = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 vel = rb.linearVelocity;
            Vector3 velAlong = Vector3.Project(vel, proj);
            float sign = Mathf.Sign(input);
            Vector3 target = proj * (sign * airMaxSpeed);
            Vector3 change = Vector3.ClampMagnitude(target - velAlong, airAccel * Time.fixedDeltaTime);
            rb.linearVelocity = vel + change;
        }

        // fail conditions
        if (transform.position.y < killY || timeSinceRail > maxAirTime)
        {
            RespawnToLastSafe();
            return;
        }

        // auto re-grab only after lockout
        if (timeSinceLeaveRail >= regrabLockout &&
            TryFindNearestRailPose(transform.position, regrabDistance, out int railIdx, out float tClosest, out Vector3 posW, out Vector3 tanW, out Vector3 upW))
        {
            currentRail = railIdx;
            t = Mathf.Clamp01(tClosest);
            rb.isKinematic = true;
            rb.useGravity = false;

            Vector3 rightW = Vector3.Cross(upW, tanW).normalized;
            Vector3 offsetDir = (offsetAxis == OffsetAxis.Up) ? upW : rightW;
            posW += offsetDir * surfaceOffset;

            rb.MovePosition(posW);
            rb.MoveRotation(Quaternion.LookRotation(tanW, useSplineUp ? upW : Vector3.up));
            mode = Mode.OnRail;

            v = 0f;
            timeSinceRail = 0f;
            timeSinceLeaveRail = 999f;
            return;
        }

        // too far from any rail? fail
        if (!TryFindNearestRailPose(transform.position, maxDetachDistance, out _, out _, out _, out _, out _))
        {
            RespawnToLastSafe();
        }
    }

    void EnterAir(Vector3 tanW, Vector3 upWorld)
    {
        mode = Mode.InAir;
        rb.isKinematic = false;
        rb.useGravity = true;

        Vector3 initial = tanW.normalized * (v + jumpForwardBoost) + upWorld.normalized * jumpImpulse;
        rb.linearVelocity = initial;

        timeSinceRail = 0f;
        timeSinceLeaveRail = 0f;
    }

    void RespawnToLastSafe()
    {
        currentRail = Mathf.Clamp(safeRailIdx, 0, rails.Count - 1);
        t = Mathf.Clamp01(safeT);

        if (!ValidateCurrent(out var s, out var world))
            return;

        float3 pL = SplineUtility.EvaluatePosition(s, t);
        SplineUtility.Evaluate(s, t, out _, out float3 tanL, out float3 upL);

        Vector3 posW = rails[currentRail].container.transform.TransformPoint(ToV3(pL));
        Vector3 tanW = NormalizeSafe(rails[currentRail].container.transform.TransformDirection(ToV3(tanL)), Vector3.forward);
        Vector3 upW = useSplineUp
            ? rails[currentRail].container.transform.TransformDirection((Vector3)math.normalizesafe(upL, new float3(0, 1, 0)))
            : Vector3.up;

        Vector3 rightW = Vector3.Cross(upW, tanW).normalized;
        Vector3 offsetDir = (offsetAxis == OffsetAxis.Up) ? upW : rightW;
        posW += offsetDir * surfaceOffset;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.MovePosition(posW);
        rb.MoveRotation(Quaternion.LookRotation(tanW, useSplineUp ? upW : Vector3.up));

        v = 0f;
        mode = Mode.OnRail;
        timeSinceRail = 0f;
        timeSinceLeaveRail = 999f;
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
        try { axis = Input.GetAxisRaw(moveAxis); } catch { axis = 0f; }

        if (legacyKeyFallback && Mathf.Approximately(axis, 0f))
        {
            bool f = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool b = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            axis = (f == b) ? 0f : (f ? 1f : -1f);
        }

        return Mathf.Clamp(axis, -1f, 1f);
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

        int knotCount = spline.Count;
        float lenLocal = 0f, lenWorld = 0f;
        try { lenLocal = SplineUtility.CalculateLength(spline, Matrix4x4.identity); } catch { }
        try { lenWorld = SplineUtility.CalculateLength(spline, world); } catch { }

        if (lenWorld <= 1e-4f)
        {
            Debug.LogError($"[RailRider] '{r.container.name}' (spline {sIdx}) knots={knotCount}, lenLocal={lenLocal:F6}, lenWorld={lenWorld:F6}. Check parent scale / duplicate knots.");
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

    // Nearest point on ANY rail within maxDist (sampling-based, no package extras needed)
    bool TryFindNearestRailPose(Vector3 posW, float maxDist, out int railIdx, out float tClosest, out Vector3 nearestPosW, out Vector3 tanW, out Vector3 upW)
    {
        railIdx = -1; tClosest = 0f; nearestPosW = Vector3.zero; tanW = Vector3.forward; upW = Vector3.up;
        float best = maxDist;

        for (int i = 0; i < rails.Count; i++)
        {
            var r = rails[i];
            if (!r.container || r.container.Splines == null || r.container.Splines.Count == 0) continue;

            int sIdx = Mathf.Clamp(r.splineIndex, 0, r.container.Splines.Count - 1);
            var s = r.container.Splines[sIdx];

            if (FindNearestOnSplineSampled(r.container.transform, s, posW, out float tCand, out Vector3 pCandW, out Vector3 tanCandW, out Vector3 upCandW))
            {
                float d = Vector3.Distance(pCandW, posW);
                if (d < best)
                {
                    best = d;
                    railIdx = i;
                    tClosest = tCand;
                    nearestPosW = pCandW;
                    tanW = tanCandW;
                    upW = upCandW;
                }
            }
        }
        return railIdx >= 0;
    }

    // Sampling-based nearest (coarse + local refine)
    bool FindNearestOnSplineSampled(Transform container, Spline s, Vector3 posW,
        out float tBest, out Vector3 pBestW, out Vector3 tanBestW, out Vector3 upBestW)
    {
        tBest = 0f; pBestW = Vector3.zero; tanBestW = Vector3.forward; upBestW = Vector3.up;

        if (s == null) return false;

        const int coarse = 64;
        float best = float.MaxValue;

        // coarse scan
        for (int i = 0; i <= coarse; i++)
        {
            float tt = i / (float)coarse;
            float3 pL = SplineUtility.EvaluatePosition(s, tt);
            Vector3 pW = container.transform.TransformPoint(ToV3(pL));
            float d = (pW - posW).sqrMagnitude;
            if (d < best)
            {
                best = d;
                tBest = tt;
                pBestW = pW;
            }
        }

        // small local refine around tBest
        const int refineIters = 3;
        const float window = 0.05f;
        for (int r = 0; r < refineIters; r++)
        {
            float tMin = Mathf.Clamp01(tBest - window / Mathf.Pow(2, r + 1));
            float tMax = Mathf.Clamp01(tBest + window / Mathf.Pow(2, r + 1));

            const int steps = 16;
            for (int i = 0; i <= steps; i++)
            {
                float tt = Mathf.Lerp(tMin, tMax, i / (float)steps);
                float3 pL = SplineUtility.EvaluatePosition(s, tt);
                Vector3 pW = container.transform.TransformPoint(ToV3(pL));
                float d = (pW - posW).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    tBest = tt;
                    pBestW = pW;
                }
            }
        }

        // orientation at tBest
        SplineUtility.Evaluate(s, tBest, out _, out float3 tanL, out float3 upL);
        tanBestW = NormalizeSafe(container.transform.TransformDirection(ToV3(tanL)), Vector3.forward);
        upBestW = useSplineUp
            ? container.transform.TransformDirection((Vector3)math.normalizesafe(upL, new float3(0, 1, 0)))
            : Vector3.up;

        return true;
    }

    // ---------- Utilities ----------
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
        mode = Mode.OnRail;

        safeRailIdx = currentRail;
        safeT = t;
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
