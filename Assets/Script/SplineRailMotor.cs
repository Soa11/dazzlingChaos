using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

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

    [Header("Ride Height (offset from centerline)")]
    // Set to tubeRadius + playerRadius (e.g., 0.5f + 0.5f = 1.0f)
    public float surfaceOffset = 1.0f;
    public enum OffsetAxis { Up, Right }           // choose which axis to offset along
    public OffsetAxis offsetAxis = OffsetAxis.Up;  // Up = sit "on top" of the tube

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
        rb.isKinematic = true;               // you’re driving with MovePosition/Rotation
        rb.useGravity = false;               // we apply gravity along the rail ourselves

        if (!splineContainer)
        {
            Debug.LogError("Assign a SplineContainer to SplineRailMotor.");
            enabled = false; return;
        }

        var spline = splineContainer.Splines[splineIndex];
        var world = splineContainer.transform.localToWorldMatrix;
        splineLength = Mathf.Max(0.001f, SplineUtility.CalculateLength(spline, world));

        t = Mathf.Repeat(t, 1f);
        lastT = t;
    }

    void FixedUpdate()
    {
        var spline = splineContainer.Splines[splineIndex];

        // --- input → target speed
        float input = Input.GetAxisRaw("Vertical");
        float target = input * maxSpeed;
        float a = (Mathf.Abs(target) > Mathf.Abs(v)) ? accel : brake;
        v = Mathf.MoveTowards(v, target, a * Time.fixedDeltaTime);

        // --- local tangent (normalized)
        float3 tanLocal = SplineUtility.EvaluateTangent(spline, t);
        float3 tanLocalN = math.lengthsq(tanLocal) > 1e-8f ? math.normalize(tanLocal) : new float3(0, 0, 1);

        // project gravity along WORLD tangent
        Vector3 tanWorld = splineContainer.transform.TransformDirection(V3(tanLocalN));
        float gAlong = Vector3.Dot(Physics.gravity, tanWorld);
        v += gAlong * gravityAlongRail * Time.fixedDeltaTime;

        // --- advance along spline
        float deltaNormalized = (v * Time.fixedDeltaTime) / splineLength;
        t = loop ? Mathf.Repeat(t + deltaNormalized, 1f) : Mathf.Clamp01(t + deltaNormalized);

        // --- evaluate full frame at new t
        SplineUtility.Evaluate(spline, t, out float3 posL, out float3 tanL2, out float3 upL); // local space
        Vector3 posWorld = splineContainer.transform.TransformPoint(V3(posL));
        Vector3 tanWorld2 = splineContainer.transform.TransformDirection(V3(math.normalize(tanL2)));
        Vector3 upWorld = splineContainer.transform.TransformDirection(V3(math.normalize(upL)));

        // choose offset axis (use 'right' if you want to ride along the tube's side)
        Vector3 rightWorld = Vector3.Cross(upWorld, tanWorld2).normalized;
        Vector3 offsetDir = (offsetAxis == OffsetAxis.Up) ? upWorld : rightWorld;

        // --- apply surface offset so we sit on top of the mesh
        Vector3 targetPos = posWorld + offsetDir * surfaceOffset;

        // --- orientation and move
        Quaternion rot = alignToTangent ? Quaternion.LookRotation(tanWorld2, upWorld) : rb.rotation;
        rb.MovePosition(targetPos);
        rb.MoveRotation(rot);

        // --- visual rolling
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
