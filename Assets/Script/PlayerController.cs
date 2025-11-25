using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;   // for float3, SplineUtility

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerController : MonoBehaviour
{
    // ---------- Types ----------
    [System.Serializable]
    public struct RailRef
    {
        public SplineContainer container; // L01, L02, ... L07
        public int splineIndex;           // always 0
    }

    // ---------- Inspector ----------
    [Header("Rails (L01–L07)")]
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
    public float intersectionStopDuration = 2f;  // seconds stopped at intersection

    [Header("Random spawn")]
    public bool useRandomSpawnPoint = true;
    public List<Transform> spawnPoints = new List<Transform>(); // 7 spawn transforms

    // ---------- Runtime ----------
    Rigidbody rb;

    public int CurrentRailIndex { get; private set; } = 0;
    public float T { get; private set; } = 0f; // normalized 0–1 along rail
    public bool IsLocked { get; private set; } = true;

    float vAlong = 0f;

    // Intersection state
    IntersectionNode currentIntersection;
    bool insideIntersection = false;
    float intersectionMoveResumeTime = 0f;

    bool IsMovementPaused => Time.time < intersectionMoveResumeTime;

    // ---------------------------------------------------------
    //                       AWAKE
    // ---------------------------------------------------------
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rails.Count == 0)
        {
            Debug.LogError("[PlayerController] No rails assigned.");
            enabled = false;
            return;
        }

        // -----------------------------------------------------
        // RANDOM SPAWNING
        // -----------------------------------------------------
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

        // -----------------------------------------------------
        // FIND NEAREST RAIL FROM SPAWN
        // -----------------------------------------------------
        if (TryFindNearestRail(transform.position, out int idx, out float tNearest))
        {
            CurrentRailIndex = idx;
            T = Mathf.Clamp01(tNearest);

            var rr = rails[idx];
            var sp = rr.container.Splines[rr.splineIndex];

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
    //                       UPDATE
    // ---------------------------------------------------------
    void Update()
    {
        if (!insideIntersection || currentIntersection == null)
            return;

        // Press A or LeftArrow to switch rails
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SwitchAtIntersection(currentIntersection);
        }
    }

    // ---------------------------------------------------------
    //             MOVEMENT WHILE LOCKED TO RAIL
    // ---------------------------------------------------------
    void TickLocked()
    {
        if (!ValidateRail(CurrentRailIndex)) return;

        var rr = rails[CurrentRailIndex];
        var sp = rr.container.Splines[rr.splineIndex];
        var wM = rr.container.transform.localToWorldMatrix;

        float input = Input.GetAxis("Vertical");
        if (Mathf.Abs(input) < inputDeadzone) input = 0f;

        float targetSpeed = IsMovementPaused ? 0f : input * maxSpeed;

        if (IsMovementPaused)
            vAlong = 0f;
        else
            vAlong = Mathf.MoveTowards(vAlong, targetSpeed, accel * Time.fixedDeltaTime);

        float length = Mathf.Max(0.001f, SplineUtility.CalculateLength(sp, wM));
        T += (vAlong * Time.fixedDeltaTime) / length;
        T = Mathf.Clamp01(T);

        // Position + tangent
        Vector3 railPos = rr.container.transform.TransformPoint(
            (Vector3)SplineUtility.EvaluatePosition(sp, T)
        ) + playerOffset;

        Vector3 tangent = rr.container.transform.TransformDirection(
            (Vector3)SplineUtility.EvaluateTangent(sp, T)
        ).normalized;

        // Spring to rail
        Vector3 toTarget = railPos - rb.position;
        Vector3 springAccel = posSpring * toTarget - posDamping * rb.linearVelocity;
        rb.AddForce(springAccel, ForceMode.Acceleration);

        // Along‐spline driving
        float vNow = Vector3.Dot(rb.linearVelocity, tangent);
        rb.AddForce(tangent * ((vAlong - vNow) / Time.fixedDeltaTime), ForceMode.Acceleration);

        // Rotate to tangent
        Quaternion want = Quaternion.LookRotation(tangent, Vector3.up);
        Quaternion dq = want * Quaternion.Inverse(rb.rotation);
        dq.ToAngleAxis(out float ang, out Vector3 axis);

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
        var node = other.GetComponent<IntersectionNode>();
        if (!node) return;

        insideIntersection = true;
        currentIntersection = node;

        intersectionMoveResumeTime = Time.time + intersectionStopDuration;
    }

    void OnTriggerExit(Collider other)
    {
        var node = other.GetComponent<IntersectionNode>();
        if (!node) return;

        if (node == currentIntersection)
        {
            insideIntersection = false;
            currentIntersection = null;
        }
    }

    // ---------------------------------------------------------
    //                SWITCH TO OTHER RAIL
    // ---------------------------------------------------------
    void SwitchAtIntersection(IntersectionNode node)
    {
        var currentRail = rails[CurrentRailIndex].container;

        if (!node.GetOther(currentRail, out var otherContainer, out int otherIndex))
            return; // this intersection doesn't connect this rail

        // find rail in list
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

        var rr = rails[targetIndex];
        var sp = rr.container.Splines[rr.splineIndex];

        // nearest point on new rail
        Vector3 worldPos = transform.position;
        Vector3 localPos = rr.container.transform.InverseTransformPoint(worldPos);

        float3 nL; float t;
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
    //                    RAIL HELPERS
    // ---------------------------------------------------------
    bool TryFindNearestRail(Vector3 pos, out int bestIdx, out float bestT)
    {
        bestIdx = -1; bestT = 0f;
        float bestDist = float.MaxValue;

        for (int i = 0; i < rails.Count; i++)
        {
            if (!ValidateRail(i)) continue;

            var rr = rails[i];
            var sp = rr.container.Splines[rr.splineIndex];

            Vector3 qL = rr.container.transform.InverseTransformPoint(pos);
            float3 nL; float t;
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
        var r = rails[i];
        if (!r.container) return false;

        var list = r.container.Splines;
        return r.splineIndex >= 0 && r.splineIndex < list.Count;
    }
}
