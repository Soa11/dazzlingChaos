// 12/11/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

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
    public Vector3 playerOffset = Vector3.zero; // Offset to prevent player from getting stuck in splines

    private Rigidbody rb;
    public int CurrentRailIndex { get; private set; } = 0; // Exposed as a public property
    public float NormalizedT { get; private set; } = 0f; // Exposed as a public property
    private float speed = 0f;
    private bool isOnRail = true;

    public bool IsOnRail => isOnRail; // Public property for camera script

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
        if (isOnRail)
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

        if (Input.GetKeyDown(KeyCode.Space))
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange); // Use AddForce instead of velocity
        }

        if (TrySnapToNearestRail())
        {
            isOnRail = true;
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
        isOnRail = false;
        rb.isKinematic = false; // Ensure the Rigidbody is affected by physics
        rb.useGravity = true;   // Enable gravity
        rb.AddForce(tangent * speed, ForceMode.VelocityChange); // Use AddForce instead of velocity
    }

    bool TrySnapToNextRail(Vector3 currentPosition)
    {
        for (int i = 0; i < rails.Count; i++)
        {
            if (i == CurrentRailIndex) continue;

            var nextRail = rails[i];
            var spline = nextRail.container.Splines[nextRail.splineIndex];
            var worldMatrix = nextRail.container.transform.localToWorldMatrix;

            float startDistance = Vector3.Distance(currentPosition, nextRail.container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, 0f)));
            float endDistance = Vector3.Distance(currentPosition, nextRail.container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, 1f)));

            if (startDistance < snapDistance || endDistance < snapDistance)
            {
                CurrentRailIndex = i;
                NormalizedT = startDistance < endDistance ? 0f : 1f;
                return true;
            }
        }
        return false;
    }

    bool TrySnapToNearestRail()
    {
        float closestDistance = snapDistance;
        int closestRailIndex = -1;
        float closestT = 0f;

        for (int i = 0; i < rails.Count; i++)
        {
            var rail = rails[i];
            var spline = rail.container.Splines[rail.splineIndex];
            var worldMatrix = rail.container.transform.localToWorldMatrix;

            for (float tt = 0; tt <= 1f; tt += 0.05f)
            {
                Vector3 position = rail.container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, tt));
                float distance = Vector3.Distance(transform.position, position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRailIndex = i;
                    closestT = tt;
                }
            }
        }

        if (closestRailIndex >= 0)
        {
            CurrentRailIndex = closestRailIndex;
            NormalizedT = closestT;
            var rail = rails[CurrentRailIndex];
            var spline = rail.container.Splines[rail.splineIndex];
            var worldMatrix = rail.container.transform.localToWorldMatrix;

            Vector3 position = rail.container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, NormalizedT)) + playerOffset;
            Vector3 tangent = rail.container.transform.TransformDirection(SplineUtility.EvaluateTangent(spline, NormalizedT));

            rb.MovePosition(position);
            rb.MoveRotation(Quaternion.LookRotation(tangent, Vector3.up));
            return true;
        }

        return false;
    }

    void RespawnToStart()
    {
        CurrentRailIndex = 0;
        NormalizedT = 0f;

        var rail = rails[CurrentRailIndex];
        var spline = rail.container.Splines[rail.splineIndex];
        var worldMatrix = rail.container.transform.localToWorldMatrix;

        Vector3 position = rail.container.transform.TransformPoint(SplineUtility.EvaluatePosition(spline, NormalizedT)) + playerOffset;
        Vector3 tangent = rail.container.transform.TransformDirection(SplineUtility.EvaluateTangent(spline, NormalizedT));

        rb.isKinematic = true;
        rb.useGravity = false;

        rb.MovePosition(position);
        rb.MoveRotation(Quaternion.LookRotation(tangent, Vector3.up));
        speed = 0f;
        isOnRail = true;
    }

    public bool ValidateRail(int railIndex)
    {
        if (rails == null || railIndex < 0 || railIndex >= rails.Count) return false;

        var rail = rails[railIndex];
        if (rail.container == null || rail.container.Splines == null || rail.container.Splines.Count == 0) return false;

        return true;
    }
}