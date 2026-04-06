using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class NetworkMobilePlayerController : NetworkBehaviour
{
    [System.Serializable]
    public struct RailRef
    {
        public SplineContainer container;
        public int splineIndex;
    }

    [Header("Path Auto-Find")]
    public string pathRootName = "PATH";
    public bool autoFindRailsFromPath = true;

    [Header("Rails (L01–L07)")]
    public List<RailRef> rails = new List<RailRef>();

    [Header("Movement along rail")]
    public float maxSpeed = 12f;
    public float accel = 12f;
    public float inputDeadzone = 0.08f;

    [Header("Rail spring forces")]
    public float posSpring = 80f;
    public float posDamping = 12f;
    public float rotTorque = 28f;
    public float rotDamping = 8f;

    [Header("Offsets")]
    public Vector3 playerOffset = Vector3.zero;

    [Header("Phone Angle Control")]
    [Tooltip("90 = stop. Less than 90 = forward. Greater than 90 = backward.")]
    public float neutralAngle = 90f;

    [Tooltip("How far from neutral before full input is reached.")]
    public float angleRange = 45f;

    [Tooltip("Smooths the tilt input.")]
    public float inputSmoothing = 6f;

    [Tooltip("Invert if your device direction feels opposite.")]
    public bool invertForwardBackward = false;

    private Rigidbody rb;

    public int CurrentRailIndex { get; private set; } = 0;
    public float T { get; private set; } = 0f;
    public bool IsLocked { get; private set; } = true;

    private float vAlong = 0f;
    private bool initialized = false;
    private float smoothedForwardInput = 0f;

    // private IntersectionNode currentIntersection;
    // private bool insideIntersection = false;
    // private float intersectionMoveResumeTime = 0f;
    // private bool intersectionTiltUsed = false;

    // private bool IsMovementPaused => Time.time < intersectionMoveResumeTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (autoFindRailsFromPath)
            AutoAssignRailsFromPath();

        if (!IsOwner)
        {
            rb.isKinematic = true;
            return;
        }

        rb.isKinematic = false;

        if (rails.Count == 0)
        {
            Debug.LogError("[NetworkMobilePlayerController] No rails assigned or found.");
            enabled = false;
            return;
        }

        initialized = true;
    }

    private void AutoAssignRailsFromPath()
    {
        bool alreadyAssigned = false;
        for (int i = 0; i < rails.Count; i++)
        {
            if (rails[i].container != null)
            {
                alreadyAssigned = true;
                break;
            }
        }

        if (alreadyAssigned)
            return;

        GameObject pathRoot = GameObject.Find(pathRootName);
        if (pathRoot == null)
        {
            Debug.LogError($"[NetworkMobilePlayerController] Could not find path root named '{pathRootName}' in the scene.");
            return;
        }

        rails.Clear();

        SplineContainer[] splineContainers = pathRoot.GetComponentsInChildren<SplineContainer>(true);

        if (splineContainers == null || splineContainers.Length == 0)
        {
            Debug.LogError($"[NetworkMobilePlayerController] No SplineContainer found under '{pathRootName}'.");
            return;
        }

        List<SplineContainer> sorted = new List<SplineContainer>(splineContainers);
        sorted.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        foreach (SplineContainer sc in sorted)
        {
            if (sc == null)
                continue;

            if (sc.Splines == null || sc.Splines.Count == 0)
                continue;

            rails.Add(new RailRef
            {
                container = sc,
                splineIndex = 0
            });
        }

        Debug.Log($"[NetworkMobilePlayerController] Auto-assigned {rails.Count} rails from '{pathRootName}'.");
    }

    public void SetSpawnOnNearestRail(Vector3 spawnWorldPosition)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        transform.position = spawnWorldPosition;
        rb.position = spawnWorldPosition;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (TryFindNearestRail(spawnWorldPosition, out int idx, out float tNearest))
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

            Debug.Log($"[NetworkMobilePlayerController] Set spawn at rail {CurrentRailIndex}, T={T}");
        }
        else
        {
            Debug.LogWarning("[NetworkMobilePlayerController] Could not find nearest rail for player spawn.");
        }
    }

    void Update()
    {
        if (!IsOwner || !IsSpawned || !initialized)
            return;

        // Intersection logic temporarily disabled.
    }

    void FixedUpdate()
    {
        if (!IsOwner || !IsSpawned || !initialized)
            return;

        if (IsLocked)
            TickLocked();
    }

    void TickLocked()
    {
        if (!ValidateRail(CurrentRailIndex))
            return;

        var rr = rails[CurrentRailIndex];
        var sp = rr.container.Splines[rr.splineIndex];
        var wM = rr.container.transform.localToWorldMatrix;

        float input = GetForwardInput();
        float targetSpeed = input * maxSpeed;

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

    /*
    void OnTriggerEnter(Collider other)
    {
        if (!IsOwner || !IsSpawned)
            return;

        var node = other.GetComponent<IntersectionNode>();
        if (!node) return;

        insideIntersection = true;
        currentIntersection = node;

        intersectionMoveResumeTime = Time.time + intersectionStopDuration;
        intersectionTiltUsed = false;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsOwner || !IsSpawned)
            return;

        var node = other.GetComponent<IntersectionNode>();
        if (!node) return;

        if (node == currentIntersection)
        {
            insideIntersection = false;
            currentIntersection = null;
            intersectionTiltUsed = false;
        }
    }

    void SwitchAtIntersection(IntersectionNode node)
    {
        var currentRail = rails[CurrentRailIndex].container;

        if (!node.GetOther(currentRail, out var otherContainer, out int otherIndex))
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

        if (targetIndex < 0)
            return;

        var rr = rails[targetIndex];
        var sp = rr.container.Splines[rr.splineIndex];

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
    */

    float GetForwardInput()
    {
#if UNITY_EDITOR
        float input = Input.GetAxis("Vertical");
        if (Mathf.Abs(input) < inputDeadzone)
            input = 0f;
        return input;
#else
        float phoneAngle = GetPhonePitchAngle();
        float input = (neutralAngle - phoneAngle) / Mathf.Max(1f, angleRange);

        if (invertForwardBackward)
            input = -input;

        input = Mathf.Clamp(input, -1f, 1f);

        if (Mathf.Abs(input) < inputDeadzone)
            input = 0f;

        smoothedForwardInput = Mathf.Lerp(
            smoothedForwardInput,
            input,
            inputSmoothing * Time.deltaTime
        );

        return smoothedForwardInput;
#endif
    }

    float GetPhonePitchAngle()
    {
        Vector3 acc = Input.acceleration.normalized;

        // angle from flat-up to flat-down
        // acc.z ≈ -1 when flat on table face up
        // acc.z ≈ 0 when near upright
        // acc.z ≈ +1 when flipped far toward user
        float angle = Mathf.Acos(Mathf.Clamp(-acc.z, -1f, 1f)) * Mathf.Rad2Deg;

        return angle;
    }

    bool TryFindNearestRail(Vector3 pos, out int bestIdx, out float bestT)
    {
        bestIdx = -1;
        bestT = 0f;
        float bestDist = float.MaxValue;

        for (int i = 0; i < rails.Count; i++)
        {
            if (!ValidateRail(i)) continue;

            var rr = rails[i];
            var sp = rr.container.Splines[rr.splineIndex];

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
        var r = rails[i];
        if (!r.container) return false;

        var list = r.container.Splines;
        return r.splineIndex >= 0 && r.splineIndex < list.Count;
    }
}