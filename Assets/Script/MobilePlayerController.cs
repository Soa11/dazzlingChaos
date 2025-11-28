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
    public float maxSpeed = 12f;
    public float accel = 12f;
    public float inputDeadzone = 0.05f;

    [Header("Rail spring forces")]
    public float posSpring = 80f;
    public float posDamping = 12f;
    public float rotTorque = 28f;
    public float rotDamping = 8f;

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

    // runtime calibration state
    Vector3 neutralAcceleration = Vector3.zero; // what "no movement" looks like
    bool hasNeutral = false;
    float calibrationTimer = 0f;

    // ---------- Runtime ----------
    Rigidbody rb;

    public int CurrentRailIndex { get; private set; } = 0;
    public float T { get; private set; } = 0f;
    public bool IsLocked { get; private set; } = true;

    float vAlong = 0f;

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

        // enable gyro if requested and supported
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
        // Calibration on device: capture your holding pose as "neutral"
#if !UNITY_EDITOR
        if (useTiltInput && autoCalibrateOnStart && !hasNeutral)
        {
            calibrationTimer += Time.deltaTime;
            if (calibrationTimer >= calibrationDelay)
            {
                neutralAcceleration = Input.acceleration;
                hasNeutral = true;
                // Debug.Log("Calibrated neutral tilt: " + neutralAcceleration);
            }
        }
#endif

        if (!insideIntersection || currentIntersection == null)
            return;

        // Keyboard path (Editor)
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SwitchAtIntersection(currentIntersection);
        }
#endif

        // Tilt path (device)
#if !UNITY_EDITOR
        if (useTiltInput && hasNeutral)
        {
            float turnInput = GetTurnInput(); // based on calibrated tilt x

            // Left tilt (negative) behaves like pressing A once
            if (!intersectionTiltUsed && turnInput <= -intersectionTiltThreshold)
            {
                SwitchAtIntersection(currentIntersection);
                intersectionTiltUsed = true;
            }

            // When we return toward center, allow another switch later
            if (Mathf.Abs(turnInput) < tiltDeadZone)
                intersectionTiltUsed = false;
        }
#endif
    }

    // ---------------------------------------------------------
    //             MOVEMENT WHILE LOCKED TO RAIL
    // ---------------------------------------------------------
    void TickLocked()
    {
        if (!ValidateRail(CurrentRailIndex)) return;

        RailRef rr = rails[CurrentRailIndex];
        Spline sp = rr.container.Splines[rr.splineIndex];
        Matrix4x4 wM = rr.container.transform.localToWorldMatrix;

        float input = GetForwardInput();
        float targetSpeed = IsMovementPaused ? 0f : input * maxSpeed;

        if (IsMovementPaused)
            vAlong = 0f;
        else
            vAlong = Mathf.MoveTowards(vAlong, targetSpeed, accel * Time.fixedDeltaTime);

        float length = Mathf.Max(0.001f, SplineUtility.CalculateLength(sp, wM));
        T += (vAlong * Time.fixedDeltaTime) / length;
        T = Mathf.Clamp01(T);

        Vector3 railPos = rr.container.transform.TransformPoint(
            (Vector3)SplineUtility.EvaluatePosition(sp, T)
        ) + playerOffset;

        Vector3 tangent = rr.container.transform.TransformDirection(
            (Vector3)SplineUtility.EvaluateTangent(sp, T)
        ).normalized;

        Vector3 toTarget = railPos - rb.position;
        Vector3 springAccel = posSpring * toTarget - posDamping * rb.linearVelocity;
        rb.AddForce(springAccel, ForceMode.Acceleration);

        float vNow = Vector3.Dot(rb.linearVelocity, tangent);
        rb.AddForce(tangent * ((vAlong - vNow) / Time.fixedDeltaTime), ForceMode.Acceleration);

        Quaternion want = Quaternion.LookRotation(tangent, Vector3.up);
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

        Vector3 worldPos = transform.position;
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
        transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

        float speed = Vector3.Dot(rb.linearVelocity, tangent);
        rb.linearVelocity = tangent * speed;
    }

    // ---------------------------------------------------------
    //                    INPUT HELPERS
    // ---------------------------------------------------------
    // Forward/backward: Editor uses W/S; device uses calibrated tilt
    float GetForwardInput()
    {
#if UNITY_EDITOR
        float input = Input.GetAxis("Vertical");
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

    // Left/right: Editor uses A/D; device uses calibrated tilt X (for intersections)
    float GetTurnInput()
    {
#if UNITY_EDITOR
        return Input.GetAxis("Horizontal");
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

    // Returns calibrated tilt: x = left/right, y = forward/back relative to neutral pose
    Vector2 GetTilt()
    {
        // Gyro path (if you later enable it)
        if (useGyro && SystemInfo.supportsGyroscope)
        {
            Quaternion q = Input.gyro.attitude;
            q = new Quaternion(q.x, q.y, -q.z, -q.w);

            Vector3 euler = q.eulerAngles;

            float tiltForward = Mathf.DeltaAngle(0f, euler.x) / 90f;
            float tiltSide = Mathf.DeltaAngle(0f, euler.z) / 90f;

            return new Vector2(tiltSide, -tiltForward);
        }

        // Accelerometer path with neutral calibration
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
