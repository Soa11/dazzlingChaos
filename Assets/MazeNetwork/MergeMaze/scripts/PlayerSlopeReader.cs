using UnityEngine;
using UnityEngine.Splines;

public class PlayerSlopeReader : MonoBehaviour
{
    [Header("References")]
    public NetworkMobilePlayerController playerController;

    [Header("Debug")]
    public float currentSlopeY = 0f;
    public Vector3 currentTangent = Vector3.forward;

    void Update()
    {
        if (playerController == null)
            return;

        int railIndex = playerController.CurrentRailIndex;
        float t = playerController.T;

        if (playerController.rails == null || playerController.rails.Count == 0)
            return;

        if (railIndex < 0 || railIndex >= playerController.rails.Count)
            return;

        var railRef = playerController.rails[railIndex];
        if (railRef.container == null)
            return;

        var splines = railRef.container.Splines;
        if (splines == null || splines.Count == 0)
            return;

        if (railRef.splineIndex < 0 || railRef.splineIndex >= splines.Count)
            return;

        var spline = splines[railRef.splineIndex];

        currentTangent = railRef.container.transform.TransformDirection(
            (Vector3)SplineUtility.EvaluateTangent(spline, t)
        ).normalized;

        currentSlopeY = currentTangent.y;
    }
}