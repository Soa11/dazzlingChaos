// 12/11/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // Required for float3

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [System.Serializable]
    public struct RailRef
    {
        public SplineContainer container;
        public int splineIndex;
    }

    [Header("Rails Configuration")]
    public List<RailRef> rails = new List<RailRef>();

    [Header("Movement Settings")]
    public float maxSpeed = 10f;
    public float acceleration = 5f;
    public float jumpForce = 10f;
    public float fallThreshold = -10f;
    public float snapDistance = 1.5f;

    [Header("Player Offset")]
    public Vector3 playerOffset = Vector3.zero;

    private Rigidbody rb;
    public int CurrentRailIndex { get; private set; } = 0;
    public float NormalizedT { get; private set; } = 0f;
    private float speed = 0f;
    public bool IsOnRail { get; private set; } = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (!ValidateRail(CurrentRailIndex))
        {
            Debug.LogError("Invalid rail setup. Please check the rails.");
            enabled = false;
        }
    }

    void FixedUpdate()
    {
        if (IsOnRail)
        {
            MoveAlongRail();
        }
        else
        {
            HandleAirMovement();
        }

        CheckFall();
    }

    void MoveAlongRail()
    {
        if (!ValidateRail(CurrentRailIndex)) return;

        var currentRail = rails[CurrentRailIndex];
        var spline = currentRail.container.Splines[currentRail.splineIndex];
        var worldMatrix = currentRail.container.transform.localToWorldMatrix;

        float input = Input.GetAxis("Vertical");
        speed = Mathf.MoveTowards(speed, input * maxSpeed, acceleration * Time.fixedDeltaTime);

        float deltaT = speed * Time.fixedDeltaTime / SplineUtility.CalculateLength(spline, worldMatrix);
        NormalizedT = Mathf.Clamp01(NormalizedT + deltaT);

        Vector3 position = currentRail.container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, NormalizedT)) + playerOffset;
        Vector3 tangent = currentRail.container.transform.TransformDirection(SplineUtility.EvaluateTangent(spline, NormalizedT));

        rb.MovePosition(position);
        rb.MoveRotation(Quaternion.LookRotation(tangent, Vector3.up));

        if (NormalizedT >= 1f || NormalizedT <= 0f)
        {
            if (!TrySnapToNextRail(position))
            {
                EnterAir(tangent);
            }
        }
    }

    void HandleAirMovement()
    {
        rb.useGravity = true;

        // Ensure the Rigidbody is not kinematic and has gravity enabled
        rb.isKinematic = false;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            rb.linearVelocity += Vector3.up * jumpForce;
        }

        if (TrySnapToNearestRail())
        {
            IsOnRail = true;
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void CheckFall()
    {
        if (transform.position.y < fallThreshold)
        {
            RespawnToStart();
        }
    }

    void EnterAir(Vector3 tangent)
    {
        Debug.Log("Player has entered the air state!"); // Debug message for testing
        IsOnRail = false;

        // Ensure Rigidbody settings are correct for falling
        rb.isKinematic = false;
        rb.useGravity = true;

        // Apply initial velocity to simulate falling
        rb.linearVelocity = tangent * speed;
    }

    bool TrySnapToNextRail(Vector3 currentPosition)
    {
        for (int i = 0; i < rails.Count; i++)
        {
            if (i == CurrentRailIndex) continue;

            var nextRail = rails[i];
            var spline = nextRail.container.Splines[nextRail.splineIndex];
            var worldMatrix = nextRail.container.transform.localToWorldMatrix;

            float3 nearestPoint;
            float normalizedT;
            float distance = SplineUtility.GetNearestPoint(spline, (float3)currentPosition, out nearestPoint, out normalizedT, resolution: 4, iterations: 2);

            if (distance <= snapDistance)
            {
                CurrentRailIndex = i;
                NormalizedT = normalizedT;
                return true;
            }
        }

        return false;
    }

    bool TrySnapToNearestRail()
    {
        for (int i = 0; i < rails.Count; i++)
        {
            var rail = rails[i];
            var spline = rail.container.Splines[rail.splineIndex];
            var worldMatrix = rail.container.transform.localToWorldMatrix;

            float3 nearestPoint;
            float normalizedT;
            float distance = SplineUtility.GetNearestPoint(spline, (float3)transform.position, out nearestPoint, out normalizedT, resolution: 4, iterations: 2);

            if (distance <= snapDistance)
            {
                CurrentRailIndex = i;
                NormalizedT = normalizedT;
                return true;
            }
        }

        return false;
    }

    void RespawnToStart()
    {
        if (!ValidateRail(CurrentRailIndex)) return;

        var rail = rails[CurrentRailIndex];
        var spline = rail.container.Splines[rail.splineIndex];
        var worldMatrix = rail.container.transform.localToWorldMatrix;

        float3 startPoint = SplineUtility.EvaluatePosition(spline, 0f);
        Vector3 startPosition = rail.container.transform.TransformPoint((Vector3)startPoint) + playerOffset;

        rb.isKinematic = true;
        rb.useGravity = false;
        transform.position = startPosition;
        NormalizedT = 0f;
        speed = 0f;
        IsOnRail = true;
    }

    public bool ValidateRail(int railIndex)
    {
        if (railIndex < 0 || railIndex >= rails.Count) return false;

        var rail = rails[railIndex];
        return rail.container != null &&
               rail.container.Splines != null &&
               rail.container.Splines.Count > rail.splineIndex &&
               rail.splineIndex >= 0;
    }
}