using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;   // for float3, SplineUtility

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class MobilePlayerController : MonoBehaviour
{
    // ---------- Types ----------
    [System.Serializable]
    public struct RailRef
    {
        public SplineContainer container; // L01, L02, ... L07
        public int splineIndex;           // always 0
    }

    // ---------- Inspector ----------
    [Header("Rails (L01-L07)")]
    public List<RailRef> rails = new List<RailRef>();

    [Header("Movement along rail")]
    public float maxSpeed = 16f;
    public float accel = 18f;
    public float inputDeadzone = 0.05f;

    [Header("Slope / Coaster Feel")]
    [Tooltip("How strongly slopes affect speed. Larger = more dramatic uphill/downhill difference.")]
    public float slopeSpeedFactor = 12f;   // try 10–18

    [Header("Rail spring forces")]
    public float posSpring = 80f;
    public float posDamping = 12f;
    public float rotTorque = 28f;
    public float rotDamping = 8f;

    [Header("Visual banking")]
    [Tooltip("Max visual lean angle (deg) left/right based on horizontal input/tilt.")]
    public float maxBankAngle = 10f;

    [Header("Offsets")]
    public Vector3 playerOffset = Vector3.zero;

    [Header("Intersection stop")]
    public float intersectionStopDuration = 2f;

    [Header("Random spawn")]
    public bool useRandomSpawnPoint = true;
    public List<Transform> spawnPoints = new List<Transform>(); // 7 spawn transforms

    [Header("Mobile Tilt Settings")]
    public bool useTiltInput = true;
    public bool useGyro = false;          // start with accelerometer only
    public float tiltDeadZone = 0.05f;    // small movements ignored
    public float maxTilt = 0.4f;          // tilt that counts as "full" input
    public float intersectionTiltThreshold = 0.25f;

    [Header("Tilt Calibration")]
    [Tooltip("Capture the phone's tilt as neutral a short time after start")]
    public bool autoCalibrateOnStart = true;

    [Tooltip("How long after start (seconds) before we capture neutral tilt")]
    public float calibrationDelay = 1f;

    [Tooltip("Flip this if forward/back feels reversed")]
    public bool invertForwardTilt = false;

    // runtime calibration state (mobile)
    Vector3 neutralAcceleration = Vector3.zero;
    bool hasNeutral = false;
    float calibrationTimer = 0f;

    // ---------- Runtime ----------
    Rigidbody rb;

    public int CurrentRailIndex { get; private set; } = 0;
    public float T { get; private set; } = 0f;   // 0–1 along current spline
    public bool IsLocked { get; private set; } = true;

    float vAlong = 0f; // desired speed along track

    IntersectionNode currentIntersection;
    bool insideIntersection = false;
    float intersectionMoveResumeTime = 0f;
    bool intersectionTiltUsed = false;

    bool IsMovementPaused { get { return Time.time < intersectionMoveResumeTime; } }

    // ---------------------------------------------------------
    //                       AWAKE
    // ---------------------------------------------------------
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rails.Count == 0)
        {
            Debug.LogError("[MobilePlayerController] No rails assigned.");
            enabled = false;
            return;
        }

        if (useGyro && SystemInfo.supportsGyroscope)
            Input.gyro.enabled = true;

        // Random spawn
        if (useRandomSpawnPoint && spawnPoints.Count > 0)
        {
            int spawnIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
            Transform sp = spawnPoints[spawnIndex];

            if (sp != null)
            {
                transform.position = sp.position;
                rb.position = sp.position;
            }
        }

        // Find nearest rail
        int idx;
        float tNearest;
        if (TryFindNearestRail(transform.position, out idx, out tNearest))
        {
            CurrentRailIndex = idx;
            T = Mathf.Clamp01(tNearest);

            RailRef rr = rails[idx];
            Spline sp = rr.container.Splines[rr.splineIndex];

            Vector3 pos = rr.container.transform.TransformPoint(
                (Vector3)SplineUtility.EvaluatePosition(sp, T)
            ) + playerOffset;

            Vector3 tan = rr.container.transform.TransformDirection(
                (Vector3)SplineUtility.EvaluateTangent(sp, T)
            ).normalized;

            transform.position = pos;
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(tan, Vector3.up);
            IsLocked = true;
        }
    }

    // ---------------------------------------------------------
    //                     FIXED UPDATE
    // ---------------------------------------------------------
    void FixedUpdate()
    {
        if (IsLocked)
            TickLocked();
    }

    // ---------------------------------------------------------
    //                        UPDATE
    // ---------------------------------------------------------
    void Update()
    {
#if !UNITY_EDITOR
        // Mobile: calibrate neutral tilt after a short delay
        if (useTiltInput && autoCalibrateOnStart && !hasNeutral)
        {
            calibrationTimer += Time.deltaTime;
            if (calibrationTimer >= calibrationDelay)
            {
                neutralAcceleration = Input.acceleration;
                hasNeutral = true;
            }
        }
#endif

        if (!insideIntersection || currentIntersection == null)
            return;

        // Editor: keyboard A/D and arrows to switch
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            SwitchAtIntersection(currentIntersection);
        }
#endif

        // Mobile: horizontal tilt (left OR right) triggers intersection switch
#if !UNITY_EDITOR
        if (useTiltInput && hasNeutral)
        {
            float turnInput = GetTurnInput(); // from tilt.x

            // Either side, once per lean
            if (!intersectionTiltUsed && Mathf.Abs(turnInput) >= intersectionTiltThreshold)
            {
                SwitchAtIntersection(currentIntersection);
                intersectionTiltUsed = true;
            }

            // When they come back near center, re-arm
            if (Mathf.Abs(turnInput) < tiltDeadZone)
                intersectionTiltUsed = false;
        }
#endif
    }

    // ---------------------------------------------------------
    //           MOVEMENT WHILE LOCKED TO RAIL (COASTER FEEL)
    // ---------------------------------------------------------
    void TickLocked()
    {
        if (!ValidateRail(CurrentRailIndex)) return;

        RailRef rr = rails[CurrentRailIndex];
        Spline sp = rr.container.Splines[rr.splineIndex];
        Matrix4x4 wM = rr.container.transform.localToWorldMatrix;

        // Evaluate tangent at current T
        Vector3 tangent = rr.container.transform.TransformDirection(
            (Vector3)SplineUtility.EvaluateTangent(sp, T)
        ).normalized;

        // Slope: dot of tangent with world "down"
        // slope > 0 → downhill (pointing downwards)
        // slope < 0 → uphill
        float slope = Vector3.Dot(tangent.normalized, Vector3.down);

        // Input-based base target speed (keyboard or tilt)
        float input = GetForwardInput();
        float baseTargetSpeed = input * maxSpeed;

        // -------- Adjusted slope logic: softer uphill, stronger downhill --------
        float targetSpeed;
        if (IsMovementPaused)
        {
            targetSpeed = 0f;
        }
        else
        {
            float uphill = Mathf.Max(0f, -slope);  // 0..1 uphill
            float downhill = Mathf.Max(0f, slope);  // 0..1 downhill

            // Softer uphill punishment
            float uphillPunish = 1f - uphill * slopeSpeedFactor * 0.10f;

            // Stronger downhill boost
            float downhillBoost = 1f + downhill * slopeSpeedFactor * 0.08f;

            float slopeFactor = uphillPunish * downhillBoost;

            // Never fully kill motion, allow stronger boost
            slopeFactor = Mathf.Clamp(slopeFactor, 0.2f, 4f);

            targetSpeed = baseTargetSpeed * slopeFactor;
        }

        // Smooth vAlong towards target
        if (IsMovementPaused)
            vAlong = 0f;
        else
            vAlong = Mathf.MoveTowards(vAlong, targetSpeed, accel * Time.fixedDeltaTime);

        // Advance T along the spline based on vAlong
        float length = Mathf.Max(0.001f, SplineUtility.CalculateLength(sp, wM));
        T += (vAlong * Time.fixedDeltaTime) / length;
        T = Mathf.Clamp01(T);

        // Position + tangent at new T
        Vector3 railPos = rr.container.transform.TransformPoint(
            (Vector3)SplineUtility.EvaluatePosition(sp, T)
        ) + playerOffset;

        tangent = rr.container.transform.TransformDirection(
            (Vector3)SplineUtility.EvaluateTangent(sp, T)
        ).normalized;

        // Spring to rail
        Vector3 toTarget = railPos - rb.position;
        Vector3 springAccel = posSpring * toTarget - posDamping * rb.linearVelocity;
        rb.AddForce(springAccel, ForceMode.Acceleration);

        // Drive RB velocity along tangent to match vAlong
        float vNow = Vector3.Dot(rb.linearVelocity, tangent);
        rb.AddForce(tangent * ((vAlong - vNow) / Time.fixedDeltaTime), ForceMode.Acceleration);

        // Rotation + banking
        float bankInput = Mathf.Clamp(GetTurnInput(), -1f, 1f);
        float bankAngle = bankInput * maxBankAngle;
        Vector3 bankedUp = Quaternion.AngleAxis(bankAngle, tangent) * Vector3.up;

        Quaternion want = Quaternion.LookRotation(tangent, bankedUp);
        Quaternion dq = want * Quaternion.Inverse(rb.rotation);
        float ang;
        Vector3 axis;
        dq.ToAngleAxis(out ang, out axis);

        if (ang > 180f) ang -= 360f;

        if (Mathf.Abs(ang) > 0.001f)
        {
            Vector3 torque = axis.normalized *
                             (rotTorque * Mathf.Deg2Rad * ang)
                             - rotDamping * rb.angularVelocity;

            rb.AddTorque(torque, ForceMode.Acceleration);
        }
    }

    // ---------------------------------------------------------
    //                     INTERSECTION TRIGGERS
    // ---------------------------------------------------------
    void OnTriggerEnter(Collider other)
    {
        IntersectionNode node = other.GetComponent<IntersectionNode>();
        if (!node) return;

        insideIntersection = true;
        currentIntersection = node;

        intersectionMoveResumeTime = Time.time + intersectionStopDuration;
        intersectionTiltUsed = false;
    }

    void OnTriggerExit(Collider other)
    {
        IntersectionNode node = other.GetComponent<IntersectionNode>();
        if (!node) return;

        if (node == currentIntersection)
        {
            insideIntersection = false;
            currentIntersection = null;
            intersectionTiltUsed = false;
        }
    }

    // ---------------------------------------------------------
    //                SWITCH TO OTHER RAIL
    // ---------------------------------------------------------
    void SwitchAtIntersection(IntersectionNode node)
    {
        SplineContainer currentRail = rails[CurrentRailIndex].container;

        SplineContainer otherContainer;
        int otherIndex;
        if (!node.GetOther(currentRail, out otherContainer, out otherIndex))
            return;

        int targetIndex = -1;
        for (int i = 0; i < rails.Count; i++)
        {
            if (rails[i].container == otherContainer &&
                rails[i].splineIndex == otherIndex)
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex < 0) return;

        RailRef rr = rails[targetIndex];
        Spline sp = rr.container.Splines[rr.splineIndex];

        Vector3 worldPos = transform.position - playerOffset;
        Vector3 localPos = rr.container.transform.InverseTransformPoint(worldPos);

        float3 nL;
        float t;
        SplineUtility.GetNearestPoint(sp, (float3)localPos, out nL, out t);

        T = Mathf.Clamp01(t);
        CurrentRailIndex = targetIndex;

        Vector3 newWorldPos = rr.container.transform.TransformPoint((Vector3)nL) + playerOffset;
        Vector3 tangent = rr.container.transform.TransformDirection(
            (Vector3)SplineUtility.EvaluateTangent(sp, T)
        ).normalized;

        rb.position = newWorldPos;
        transform.position = newWorldPos;

        float speed = Vector3.Dot(rb.linearVelocity, tangent);
        rb.linearVelocity = tangent * speed;

        transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
    }

    // ---------------------------------------------------------
    //                    INPUT HELPERS
    // ---------------------------------------------------------
    float GetForwardInput()
    {
#if UNITY_EDITOR
        float input = Input.GetAxis("Vertical");   // W/S, Up/Down
        if (Mathf.Abs(input) < inputDeadzone) input = 0f;
        return input;
#else
        if (!useTiltInput)
        {
            float input = Input.GetAxis("Vertical");
            if (Mathf.Abs(input) < inputDeadzone) input = 0f;
            return input;
        }

        if (!hasNeutral) return 0f; // not calibrated yet

        Vector2 tilt = GetTilt(); // x = left/right, y = forward/back relative to neutral

        float sign = invertForwardTilt ? -1f : 1f;
        float inputTilt = sign * tilt.y;

        if (Mathf.Abs(inputTilt) < tiltDeadZone)
            inputTilt = 0f;

        if (maxTilt > 0f)
            inputTilt = Mathf.Clamp(inputTilt / maxTilt, -1f, 1f);

        return inputTilt;
#endif
    }

    float GetTurnInput()
    {
#if UNITY_EDITOR
        float input = Input.GetAxis("Horizontal"); // A/D, Left/Right
        if (Mathf.Abs(input) < inputDeadzone) input = 0f;
        return input;
#else
        if (!useTiltInput)
            return Input.GetAxis("Horizontal");

        if (!hasNeutral) return 0f;

        Vector2 tilt = GetTilt();
        float x = tilt.x;

        if (Mathf.Abs(x) < tiltDeadZone)
            x = 0f;

        if (maxTilt > 0f)
            x = Mathf.Clamp(x / maxTilt, -1f, 1f);

        return x;
#endif
    }

    Vector2 GetTilt()
    {
        if (useGyro && SystemInfo.supportsGyroscope)
        {
            Quaternion q = Input.gyro.attitude;
            q = new Quaternion(q.x, q.y, -q.z, -q.w);

            Vector3 euler = q.eulerAngles;

            float tiltForward = Mathf.DeltaAngle(0f, euler.x) / 90f;
            float tiltSide = Mathf.DeltaAngle(0f, euler.z) / 90f;

            return new Vector2(tiltSide, -tiltForward);
        }

        Vector3 acc = Input.acceleration;
        Vector3 delta = acc - neutralAcceleration;

        return new Vector2(delta.x, delta.y);
    }

    // ---------------------------------------------------------
    //                    RAIL HELPERS
    // ---------------------------------------------------------
    bool TryFindNearestRail(Vector3 pos, out int bestIdx, out float bestT)
    {
        bestIdx = -1;
        bestT = 0f;
        float bestDist = float.MaxValue;

        for (int i = 0; i < rails.Count; i++)
        {
            if (!ValidateRail(i)) continue;

            RailRef rr = rails[i];
            Spline sp = rr.container.Splines[rr.splineIndex];

            Vector3 qL = rr.container.transform.InverseTransformPoint(pos);
            float3 nL;
            float t;
            SplineUtility.GetNearestPoint(sp, (float3)qL, out nL, out t);

            Vector3 nW = rr.container.transform.TransformPoint((Vector3)nL);
            float d = Vector3.Distance(pos, nW);

            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
                bestT = t;
            }
        }
        return bestIdx >= 0;
    }

    bool ValidateRail(int i)
    {
        if (i < 0 || i >= rails.Count) return false;
        RailRef r = rails[i];
        if (!r.container) return false;

        var list = r.container.Splines;
        return r.splineIndex >= 0 && r.splineIndex < list.Count;
    }
}
