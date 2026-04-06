using UnityEngine;

public class PlayerVisualLean : MonoBehaviour
{
    [Header("References")]
    public Transform playerRoot;
    public Rigidbody playerRb;

    [Header("Lean Settings")]
    public float leanAmount = 12f;
    public float leanSmooth = 6f;
    public float speedInfluence = 0.5f;

    private Quaternion baseLocalRotation;
    private Vector3 lastForward;

    void Awake()
    {
        baseLocalRotation = transform.localRotation;

        if (playerRoot == null && transform.parent != null)
            playerRoot = transform.parent;

        if (playerRb == null && playerRoot != null)
            playerRb = playerRoot.GetComponent<Rigidbody>();

        if (playerRoot != null)
            lastForward = playerRoot.forward;
    }

    void LateUpdate()
    {
        if (playerRoot == null)
            return;

        Vector3 currentForward = playerRoot.forward;

        Vector3 flatLast = Vector3.ProjectOnPlane(lastForward, Vector3.up).normalized;
        Vector3 flatCurrent = Vector3.ProjectOnPlane(currentForward, Vector3.up).normalized;

        if (flatLast.sqrMagnitude < 0.0001f || flatCurrent.sqrMagnitude < 0.0001f)
        {
            lastForward = currentForward;
            return;
        }

        float turnAmount = Vector3.SignedAngle(flatLast, flatCurrent, Vector3.up) / 30f;
        turnAmount = Mathf.Clamp(turnAmount, -1f, 1f);

        float speed01 = 0f;
        if (playerRb != null)
            speed01 = Mathf.Clamp01(playerRb.linearVelocity.magnitude / 10f);

        float targetRoll = -turnAmount * leanAmount * (1f + speed01 * speedInfluence);

        Quaternion targetRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, targetRoll);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            leanSmooth * Time.deltaTime
        );

        lastForward = currentForward;
    }
}