using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
public class SplineSmoother : MonoBehaviour
{
    [Header("Input (original CSV spline)")]
    public SplineContainer source;

    [Header("Output (same points with smoothed tangents)")]
    public SplineContainer output;

    void Update()
    {
        if (source == null || output == null)
            return;

        var src = source.Spline;
        var dst = output.Spline;

        // Copy all knots exactly
        dst.Clear();
        for (int i = 0; i < src.Count; i++)
        {
            dst.Add(src[i]);
        }

        // Apply auto smoothing to tangents
        for (int i = 0; i < dst.Count; i++)
        {
            dst.SetTangentMode(i, TangentMode.AutoSmooth);
        }
    }
}