using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))] // needed to receive trigger events
public class PlayerMove : MonoBehaviour
{
    [System.Serializable]
    public struct RailRef
    {
        public SplineContainer container;
        public int splineIndex; // usually 0
    }

    [Header("Rails")]
    public List<RailRef> rails = new List<RailRef>();

    [Header("Movement (while locked)")]
    public float maxSpeed = 12f;
    public float accel = 12f;
    public float inputDeadzone = 0.05f;
    public float jumpImpulse = 8f;

    [Header("Attach / Detach")]
    public bool autoHopAtEnds = false;      // OFF = guaranteed drop at ends
    public float captureRadius = 1.6f;      // reattach distance while in air
    public float snapCooldown = 0.35f;      // no-capture window after drop
    public float endBias = 0.002f;          // avoid exact 0/1 evaluation
    public float fallYThreshold = -50f;

    [Header("Rail Spring (while locked)")]
    public float posSpring = 80f;
    public float posDamping = 12f;
    public float rotTorque = 28f;
    public float rotDamping = 8f;

    [Header("Offsets")]
    public Vector3 playerOffset = Vector3.zero;

    [Header("Holes (Trigger Settings)")]
    public string holeTag = "Hole";         // tag used on Hole_1..Hole_7 triggers
    public float jumpBypassDuration = 0.2f; // seconds after a jump during which holes won't drop you

    // Runtime
    Rigidbody rb;
    public int CurrentRailIndex { get; private set; } = 0;
    public float T { get; private set; } = 0f;   // normalized along current rail
    public bool IsLocked { get; private set; } = false;

    float vAlong = 0f;
    float recaptureUnblockTime = 0f;
    float jumpBypassUntil = 0f;             // “not jumping” = Time.time >= this

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rails == null || rails.Count == 0)
        {
            Debug.LogError("[PlayerMove] No rails assigned."); enabled = false; return;
        }

        // Auto-capture nearest rail at start and hard-place to avoid initial fall
        if (TryFindNearestRail(transform.position, out int idx, out float tNearest, out float _))
        {
            CurrentRailIndex = idx;
            T = Mathf.Clamp01(Mathf.Lerp(endBias, 1f - endBias, tNearest));
            IsLocked = true;

            var rr = rails[CurrentRailIndex];
            var sp = rr.container.Splines[rr.splineIndex];
            Vector3 p = rr.container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(sp, T)) + playerOffset;
            Vector3 z = rr.container.transform.TransformDirection((Vector3)SplineUtility.EvaluateTangent(sp, T)).normalized;

            transform.position = p;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(z, Vector3.up);
        }
        else
        {
            IsLocked = false; // start falling until you get near a rail
            recaptureUnblockTime = 0f;
        }
    }

    void FixedUpdate()
    {
        if (IsLocked) TickLocked();
        else TickAir();

        if (transform.position.y < fallYThreshold)
            RespawnToStart();
    }

    // ---------- LOCKED ----------
    void TickLocked()
    {
        if (!ValidateRail(CurrentRailIndex)) { Unlock(Vector3.zero); return; }

        var rr = rails[CurrentRailIndex];
        var sp = rr.container.Splines[rr.splineIndex];
        var wM = rr.container.transform.localToWorldMatrix;

        float input = Input.GetAxis("Vertical");
        if (Mathf.Abs(input) < inputDeadzone) input = 0f;

        float targetSpeed = input * maxSpeed;
        vAlong = Mathf.MoveTowards(vAlong, targetSpeed, accel * Time.fixedDeltaTime);

        float length = Mathf.Max(0.001f, SplineUtility.CalculateLength(sp, wM));
        float tPrev = T;
        float tNext = tPrev + (vAlong * Time.fixedDeltaTime) / length;

        bool crossedStart = (tNext <= 0f && tPrev > 0f);
        bool crossedEnd = (tNext >= 1f && tPrev < 1f);

        T = Mathf.Clamp01(tNext);
        T = Mathf.Clamp(T, endBias, 1f - endBias);

        Vector3 railPos = rr.container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(sp, T)) + playerOffset;
        Vector3 tangent = rr.container.transform.TransformDirection((Vector3)SplineUtility.EvaluateTangent(sp, T)).normalized;

        // Positional spring
        Vector3 toTarget = railPos - rb.position;
        Vector3 springAccel = posSpring * toTarget - posDamping * rb.linearVelocity;
        rb.AddForce(springAccel, ForceMode.Acceleration);

        // Tangential drive
        float vNow = Vector3.Dot(rb.linearVelocity, tangent);
        rb.AddForce(tangent * ((vAlong - vNow) / Time.fixedDeltaTime), ForceMode.Acceleration);

        // Orientation PD
        Quaternion want = Quaternion.LookRotation(tangent, Vector3.up);
        Quaternion dq = want * Quaternion.Inverse(rb.rotation);
        dq.ToAngleAxis(out float ang, out Vector3 axis);
        if (ang > 180f) ang -= 360f;
        if (Mathf.Abs(ang) > 0.001f)
        {
            Vector3 torque = axis.normalized * (rotTorque * Mathf.Deg2Rad * ang) - rotDamping * rb.angularVelocity;
            rb.AddTorque(torque, ForceMode.Acceleration);
        }

        // Jump
        if (Input.GetKeyDown(KeyCode.Space))
        {
            rb.linearVelocity += Vector3.up * jumpImpulse;
            jumpBypassUntil = Time.time + jumpBypassDuration; // holes won't drop during this window
        }

        // Ends
        if (crossedStart || crossedEnd)
        {
            if (autoHopAtEnds)
            {
                if (!TryHopToNeighbor(railPos))
                    Unlock(tangent);
            }
            else
            {
                Unlock(tangent);
            }
        }
    }

    // ---------- IN AIR ----------
    void TickAir()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            rb.linearVelocity += Vector3.up * jumpImpulse;
            jumpBypassUntil = Time.time + jumpBypassDuration;
        }

        if (Time.time < recaptureUnblockTime) return;

        if (TryCapture(transform.position, captureRadius, out int idx, out float t))
        {
            CurrentRailIndex = idx;
            T = Mathf.Clamp01(Mathf.Lerp(endBias, 1f - endBias, t));
            IsLocked = true; // springs will pull us in next frame
        }
    }

    // ---------- Trigger-based holes ----------
    void OnTriggerEnter(Collider other)
    {
        if (!IsLocked) return;                     // already falling
        if (!other || !other.CompareTag(holeTag)) return;

        // Only drop if "not jumping"
        if (Time.time >= jumpBypassUntil)
        {
            // Use forward as carry direction; if you want tangent, sample tangent here:
            Vector3 carry = transform.forward;
            Unlock(carry);
        }
    }

    void OnTriggerStay(Collider other)
    {
        // Extra safety: if we remain inside a hole and not jumping, still unlock
        if (!IsLocked) return;
        if (!other || !other.CompareTag(holeTag)) return;
        if (Time.time >= jumpBypassUntil)
        {
            Vector3 carry = transform.forward;
            Unlock(carry);
        }
    }

    // ---------- Helpers ----------
    void Unlock(Vector3 carryDir)
    {
        IsLocked = false;

        if (carryDir.sqrMagnitude > 0.0001f)
        {
            Vector3 dir = carryDir.normalized;
            float vNow = Vector3.Dot(rb.linearVelocity, dir);
            rb.linearVelocity += dir * (vAlong - vNow);
        }

        recaptureUnblockTime = Time.time + snapCooldown;
    }

    bool TryHopToNeighbor(Vector3 fromWorld)
    {
        int best = -1; float bestDist = captureRadius; float bestT = 0f;

        for (int i = 0; i < rails.Count; i++)
        {
            if (i == CurrentRailIndex) continue;
            if (!ValidateRail(i)) continue;

            var rr = rails[i];
            var sp = rr.container.Splines[rr.splineIndex];

            Vector3 a = rr.container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(sp, 0f));
            Vector3 b = rr.container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(sp, 1f));
            float da = Vector3.Distance(fromWorld, a);
            float db = Vector3.Distance(fromWorld, b);

            if (da < bestDist) { best = i; bestDist = da; bestT = endBias; }
            if (db < bestDist) { best = i; bestDist = db; bestT = 1f - endBias; }
        }

        if (best >= 0)
        {
            CurrentRailIndex = best;
            T = Mathf.Clamp01(bestT);
            return true;
        }
        return false;
    }

    bool TryCapture(Vector3 queryWorld, float radius, out int bestIdx, out float bestT)
    {
        bestIdx = -1; bestT = 0f;
        float bestDist = radius;

        for (int i = 0; i < rails.Count; i++)
        {
            if (!ValidateRail(i)) continue;
            var rr = rails[i];
            var sp = rr.container.Splines[rr.splineIndex];

            Vector3 qL = rr.container.transform.InverseTransformPoint(queryWorld);
            float3 nL; float t;
            SplineUtility.GetNearestPoint(sp, (float3)qL, out nL, out t, resolution: 6, iterations: 3);
            Vector3 nW = rr.container.transform.TransformPoint((Vector3)nL);
            float d = Vector3.Distance(queryWorld, nW);

            if (d < bestDist) { bestDist = d; bestIdx = i; bestT = t; }
        }
        return bestIdx >= 0;
    }

    bool TryFindNearestRail(Vector3 queryWorld, out int bestIdx, out float bestT, out float bestWorldDist)
    {
        bestIdx = -1; bestT = 0f; bestWorldDist = float.MaxValue;

        for (int i = 0; i < rails.Count; i++)
        {
            if (!ValidateRail(i)) continue;
            var rr = rails[i];
            var sp = rr.container.Splines[rr.splineIndex];

            Vector3 qL = rr.container.transform.InverseTransformPoint(queryWorld);
            float3 nL; float t;
            SplineUtility.GetNearestPoint(sp, (float3)qL, out nL, out t, resolution: 8, iterations: 4);
            Vector3 nW = rr.container.transform.TransformPoint((Vector3)nL);
            float d = Vector3.Distance(queryWorld, nW);

            if (d < bestWorldDist) { bestWorldDist = d; bestIdx = i; bestT = t; }
        }
        return bestIdx >= 0;
    }

    void RespawnToStart()
    {
        if (!ValidateRail(0)) return;
        CurrentRailIndex = 0; T = 0f; IsLocked = true; vAlong = 0f;

        var rr = rails[0]; var sp = rr.container.Splines[rr.splineIndex];
        Vector3 p = rr.container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(sp, 0f)) + playerOffset;
        Vector3 z = rr.container.transform.TransformDirection((Vector3)SplineUtility.EvaluateTangent(sp, 0f)).normalized;

        transform.position = p;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.LookRotation(z, Vector3.up);
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
