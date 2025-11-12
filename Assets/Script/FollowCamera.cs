// FollowCamera.cs � smooth dynamic follow for PlayerMove
// Drop this on your Main Camera and assign the Player (transform).
//
// Features:
// - Smooth follow using spring-damping, not parenting (physics-friendly).
// - Auto-looks toward the player's forward direction or velocity.
// - Adjustable offsets and rotation lag for cinematic feel.

using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;              // Assign Player object here.

    [Header("Offsets")]
    public Vector3 followOffset = new Vector3(0f, 3f, -8f); // relative to player
    public Vector3 lookOffset = new Vector3(0f, 1.5f, 0f); // where the camera looks

    [Header("Smoothing")]
    [Tooltip("Higher = snappier follow, Lower = smoother delay")]
    public float positionSmoothTime = 0.15f;
    public float rotationSmoothSpeed = 6f;

    [Header("Optional velocity-based look")]
    public bool useVelocityLook = true;  // when true, look in the direction player moves
    public float velocityInfluence = 0.3f; // 0 = ignore velocity, 1 = full influence

    private Vector3 velocitySmoothed;     // for SmoothDamp

    Rigidbody playerRb;                   // cached RB to read velocity

    void Start()
    {
        if (!target)
        {
            Debug.LogError("[FollowCamera] No target assigned!");
            enabled = false;
            return;
        }

        playerRb = target.GetComponent<Rigidbody>();
        // Immediately jump to correct position at start
        transform.position = target.position + target.TransformDirection(followOffset);
        transform.LookAt(target.position + lookOffset);
    }

    void LateUpdate()
    {
        if (!target) return;

        // Base desired camera position in world space
        Vector3 desiredPos = target.TransformPoint(followOffset);

        // Smoothly move to that position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocitySmoothed, positionSmoothTime);

        // Compute direction to look
        Vector3 lookDir;
        if (useVelocityLook && playerRb && playerRb.linearVelocity.sqrMagnitude > 0.1f)
        {
            // Blend between player's forward and its velocity direction
            Vector3 velDir = playerRb.linearVelocity.normalized;
            lookDir = Vector3.Lerp(target.forward, velDir, velocityInfluence);
        }
        else
        {
            lookDir = target.forward;
        }

        Vector3 lookTarget = target.position + lookOffset;
        Quaternion desiredRot = Quaternion.LookRotation(lookDir, Vector3.up);

        // Smoothly rotate toward desired rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmoothSpeed * Time.deltaTime);
    }
}
