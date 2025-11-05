using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // <-- add this

[RequireComponent(typeof(Rigidbody))]
public class SplineRailMotor : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer splineContainer;
    public int splineIndex = 0;
    public bool loop = true;

    [Header("Speed & Input")]
    public float maxSpeed = 22f;
    public float accel = 14f;
    public float brake = 18f;

    [Header("Gravity on slopes")]
    public float gravityAlongRail = 1.0f;

    [Header("Orientation")]
    public bool alignToTangent = true;
    public Vector3 upHint = Vector3.up;

    [Header("Rolling Visual (child sphere optional)")]
    public Transform rollingVisual;
    public float visualRadius = 0.5f;

    Rigidbody rb;
    float t, splineLength, v, lastT;

    static Vector3 V3(float3 f) => new Vector3(f.x, f.y, f.z);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (!splineContainer)
        {
            Debug.LogError("Assign a SplineContainer to SplineRailMotor.");
            enabled = false; return;
        }

        var spline = splineContainer.Splines[splineIndex];

        // ✅ your package wants the matrix here
        var world = splineContainer.transform.localToWorldMatrix;
        splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(spline, world));

        t = Mathf.Repeat(t, 1f);
        lastT = t;
    }

    void FixedUpdate()
    {
        var spline = splineContainer.Splines[splineIndex];

        // Input → target speed
        float input = Input.GetAxisRaw("Vertical");
        float target = input * maxSpeed;
        float a = (Mathf.Abs(target) > Mathf.Abs(v)) ? accel : brake;
        v = Mathf.MoveTowards(v, target, a * Time.fixedDeltaTime);

        // ✅ Evaluate LOCAL tangent (no matrix overload), then normalize
        float3 tanLocal = SplineUtility.EvaluateTangent(spline, t);
        float3 tanLocalN = math.lengthsq(tanLocal) > 1e-8f ? math.normalize(tanLocal) : new float3(0, 0, 1);

        // project gravity along WORLD tangent: convert to world dir
        Vector3 tanWorld = splineContainer.transform.TransformDirection(V3(tanLocalN));
        float gAlong = Vector3.Dot(Physics.gravity, tanWorld);
        v += gAlong * gravityAlongRail * Time.fixedDeltaTime;

        // Advance along spline
        float deltaNormalized = (v * Time.fixedDeltaTime) / splineLength;
        t = loop ? Mathf.Repeat(t + deltaNormalized, 1f) : Mathf.Clamp01(t + deltaNormalized);

        // ✅ Evaluate LOCAL position/tangent again and convert to WORLD
        float3 posLocal = SplineUtility.EvaluatePosition(spline, t);
        float3 tanLocal2 = SplineUtility.EvaluateTangent(spline, t);
        float3 tanLocalN2 = math.lengthsq(tanLocal2) > 1e-8f ? math.normalize(tanLocal2) : new float3(0, 0, 1);

        Vector3 posWorld = splineContainer.transform.TransformPoint(V3(posLocal));
        Vector3 tanWorld2 = splineContainer.transform.TransformDirection(V3(tanLocalN2));

        Vector3 up = upHint.normalized;
        Quaternion rot = alignToTangent ? Quaternion.LookRotation(tanWorld2, up) : rb.rotation;

        rb.MovePosition(posWorld);
        rb.MoveRotation(rot);

        if (rollingVisual && visualRadius > 0f)
        {
            float dtNorm = Mathf.DeltaAngle(lastT * 360f, t * 360f) / 360f;
            float ds = Mathf.Sign(v) * Mathf.Abs(dtNorm) * splineLength;
            float angleDeg = (ds / (visualRadius * 2f * Mathf.PI)) * 360f;
            rollingVisual.Rotate(Vector3.right, angleDeg, Space.Self);
            lastT = t;
        }
    }
}
