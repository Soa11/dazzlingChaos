using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Offsets")]
    public Vector3 followOffset = new Vector3(0f, 3f, -8f);
    public Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Smoothing")]
    public float positionSmoothTime = 0.15f;
    public float rotationSmoothSpeed = 6f;

    [Header("Velocity look")]
    public bool useVelocityLook = true;
    public float velocityInfluence = 0.3f;

    [Header("Startup / Stall guards")]
    [Tooltip("Snap (no smoothing) for at least this many seconds after Play.")]
    public float startupSnapSeconds = 0.75f;
    [Tooltip("Also keep snapping until the target is this 'stable' (m/s) for N frames.")]
    public float stableSpeedThreshold = 0.05f;
    public int stableFramesNeeded = 3;
    [Tooltip("If a frame stalls longer than this unscaled time, snap this frame.")]
    public float stallSnapThreshold = 0.25f;

    Vector3 velocitySmoothed;
    Rigidbody rb;
    float startUnscaled;
    int stableFrames;
    Vector3 lastTargetPos;

    void Start()
    {
        if (!target)
        {
            Debug.LogError("[FollowCamera] No target assigned!");
            enabled = false;
            return;
        }

        rb = target.GetComponent<Rigidbody>();
        startUnscaled = Time.unscaledTime;
        lastTargetPos = target.position;

        // Hard snap on start
        Vector3 desired = target.TransformPoint(followOffset);
        transform.position = desired;
        transform.rotation = Quaternion.LookRotation(target.forward, Vector3.up);
    }

    void LateUpdate()
    {
        if (!target) return;

        // Detect stalls & startup window
        bool inStartupWindow = (Time.unscaledTime - startUnscaled) < startupSnapSeconds;
        bool frameStalled = Time.unscaledDeltaTime > stallSnapThreshold;

        // Detect if target is still "settling" (teleporting / snapping)
        float targetSpeed = (target.position - lastTargetPos).magnitude / Mathf.Max(Time.deltaTime, 1e-5f);
        lastTargetPos = target.position;
        if (targetSpeed < stableSpeedThreshold) stableFrames++; else stableFrames = 0;
        bool targetStable = stableFrames >= stableFramesNeeded;

        // Desired pose
        Vector3 desiredPos = target.TransformPoint(followOffset);

        // Choose look direction
        Vector3 lookDir;
        if (useVelocityLook && rb && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            Vector3 velDir = rb.linearVelocity.normalized;
            lookDir = Vector3.Lerp(target.forward, velDir, velocityInfluence);
        }
        else
        {
            lookDir = target.forward;
        }
        Quaternion desiredRot = Quaternion.LookRotation(lookDir, Vector3.up);

        // SNAP while: startup window OR frame stall OR target not yet stable
        if (inStartupWindow || frameStalled || !targetStable)
        {
            transform.position = desiredPos;
            transform.rotation = desiredRot;
            return;
        }

        // Smooth after things are stable
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocitySmoothed, positionSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmoothSpeed * Time.deltaTime);
    }
}
