using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // ✅ add this

public class SplineChaseCamera : MonoBehaviour
{
    public SplineRailMotor targetMotor;    // PlayerRig’s motor
    public SplineContainer spline;         // same spline
    public float followDistance = 3f;      // behind the player along tangent
    public float lookAhead = 2f;           // meters ahead for aim
    public Vector3 offset = new Vector3(0, 1.2f, 0);
    public float positionLerp = 10f;
    public float rotationLerp = 10f;

    void LateUpdate()
    {
        if (!targetMotor || !spline) return;
        var s = spline.Splines[targetMotor.splineIndex];

        // current world pos/tan
        var tField = typeof(SplineRailMotor).GetField("t",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float tn = (float)tField.GetValue(targetMotor);

        // ✅ Evaluate in local space, then transform
        float3 posLocal = SplineUtility.EvaluatePosition(s, tn);
        float3 tanLocal = SplineUtility.EvaluateTangent(s, tn);
        float3 tanLocalN = math.lengthsq(tanLocal) > 1e-8f ? math.normalize(tanLocal) : new float3(0, 0, 1);

        Vector3 pos = spline.transform.TransformPoint((Vector3)posLocal);
        Vector3 tan = spline.transform.TransformDirection((Vector3)tanLocalN).normalized;

        // sample look-ahead by small normalized step (meters → normalized)
        float length = Mathf.Max(0.001f, SplineUtility.CalculateLength(s, spline.transform.localToWorldMatrix));
        float dn = Mathf.Clamp01(lookAhead / length);
        float tLook = (targetMotor.loop ? Mathf.Repeat(tn + dn, 1f) : Mathf.Clamp01(tn + dn));

        float3 posLocalLook = SplineUtility.EvaluatePosition(s, tLook);
        Vector3 posAhead = spline.transform.TransformPoint((Vector3)posLocalLook);

        // follow slightly behind current pos
        Vector3 followPos = pos - tan * followDistance + offset;
        transform.position = Vector3.Lerp(transform.position, followPos, 1 - Mathf.Exp(-positionLerp * Time.deltaTime));

        Quaternion targetRot = Quaternion.LookRotation((posAhead - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1 - Mathf.Exp(-rotationLerp * Time.deltaTime));
    }
}
