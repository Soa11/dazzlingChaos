using System.Collections;
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

    [Header("Rails")]
    public List<RailRef> rails = new List<RailRef>();

    [Header("Movement along rail")]
    public float maxSpeed = 18f;
    public float accel = 10f;
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
    public float angleRange = 42f;

    [Tooltip("Smooths mobile input.")]
    public float inputSmoothing = 6f;

    [Tooltip("Tick this if forward/backward feels reversed on device.")]
    public bool invertForwardBackward = false;

    [Header("Slope Speed Effect")]
    public bool useSlopeSpeedEffect = true;
    public float uphillSlowdown = 1.2f;
    public float downhillBoost = 0.25f;

    [Header("Input Start")]
    [Tooltip("If true, owner input starts automatically after delay.")]
    public bool autoEnableInput = true;

    [Tooltip("Useful if UI stays on screen for a few seconds.")]
    public float autoEnableDelay = 5f;

    [Tooltip("Read-only at runtime.")]
    public bool inputEnabled = false;

    [Header("Debug")]
    [SerializeField] private float debugPhoneAngle = 90f;
    [SerializeField] private float debugForwardInput = 0f;
    [SerializeField] private float debugSlopeY = 0f;
    [SerializeField] private float debugSlopeMultiplier = 1f;

    private Rigidbody rb;
    private bool initialized = false;
    private float vAlong = 0f;
    private float smoothedForwardInput = 0f;
    private Coroutine autoEnableRoutine;

    public int CurrentRailIndex { get; private set; } = 0;
    public float T { get; private set; } = 0f;
    public bool IsLocked { get; private set; } = true;

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

        inputEnabled = false;
        smoothedForwardInput = 0f;
        vAlong = 0f;
        initialized = true;

        if (autoEnableInput)
        {
            if (autoEnableRoutine != null)
                StopCoroutine(autoEnableRoutine);

            autoEnableRoutine = StartCoroutine(AutoEnableInputAfterDelay());
        }
    }

    IEnumerator AutoEnableInputAfterDelay()
    {
        yield return new WaitForSeconds(autoEnableDelay);
        EnableInput();
    }

    public void EnableInput()
    {
        if (!IsOwner)
            return;

        inputEnabled = true;
        smoothedForwardInput = 0f;
        vAlong = 0f;

        Debug.Log("[NetworkMobilePlayerController] Input ENABLED");
    }

    public void DisableInput()
    {
        if (!IsOwner)
            return;

        inputEnabled = false;
        smoothedForwardInput = 0f;
        vAlong = 0f;

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("[NetworkMobilePlayerController] Input DISABLED");
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
            if (sc == null) continue;
            if (sc.Splines == null || sc.Splines.Count == 0) continue;

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

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

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

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

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

#if !UNITY_EDITOR
        debugPhoneAngle = GetPhonePitchAngle();
        debugForwardInput = GetForwardInput();
#endif
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

        Vector3 tangent = rr.container.transform.TransformDirection(
            (Vector3)SplineUtility.EvaluateTangent(sp, T)
        ).normalized;

        float input = GetForwardInput();
        float slopeMultiplier = GetSlopeSpeedMultiplier(tangent.y, input);
        float targetSpeed = input * maxSpeed * slopeMultiplier;

        debugSlopeY = tangent.y;
        debugSlopeMultiplier = slopeMultiplier;

        vAlong = Mathf.MoveTowards(vAlong, targetSpeed, accel * Time.fixedDeltaTime);

        float length = Mathf.Max(0.001f, SplineUtility.CalculateLength(sp, wM));
        T += (vAlong * Time.fixedDeltaTime) / length;
        T = Mathf.Clamp01(T);

        Vector3 railPos = rr.container.transform.TransformPoint(
            (Vector3)SplineUtility.EvaluatePosition(sp, T)
        ) + playerOffset;

        tangent = rr.container.transform.TransformDirection(
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
            Vector3 torque =
                axis.normalized * (rotTorque * Mathf.Deg2Rad * ang)
                - rotDamping * rb.angularVelocity;

            rb.AddTorque(torque, ForceMode.Acceleration);
        }
    }

    float GetSlopeSpeedMultiplier(float slopeY, float input)
    {
        if (!useSlopeSpeedEffect || Mathf.Approximately(input, 0f))
            return 1f;

        float travelSlope = slopeY * Mathf.Sign(input);
        float multiplier = 1f;

        if (travelSlope > 0f)
        {
            // uphill
            multiplier -= travelSlope * uphillSlowdown;
        }
        else if (travelSlope < 0f)
        {
            // downhill
            multiplier += (-travelSlope) * downhillBoost;
        }

        return Mathf.Clamp(multiplier, 0.3f, 1.5f);
    }

    float GetForwardInput()
    {
        if (!inputEnabled)
            return 0f;

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
        else
            input = Mathf.Sign(input) * input * input;

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
        Vector3 g = Input.acceleration.normalized;

        if (g.sqrMagnitude < 0.0001f)
            return neutralAngle;

        float angle = Vector3.SignedAngle(Vector3.back, g, Vector3.right);

        if (angle < 0f) angle += 360f;
        if (angle > 180f) angle = 360f - angle;

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