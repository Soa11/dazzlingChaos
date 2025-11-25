using UnityEngine;
using UnityEngine.Splines;

public class IntersectionNode : MonoBehaviour
{
    [Header("Two lines that cross here")]
    public SplineContainer railA;
    public int splineIndexA = 0;

    public SplineContainer railB;
    public int splineIndexB = 0;

    public bool GetOther(SplineContainer current,
                         out SplineContainer other,
                         out int otherIndex)
    {
        if (current == railA)
        {
            other = railB;
            otherIndex = splineIndexB;
            return true;
        }

        if (current == railB)
        {
            other = railA;
            otherIndex = splineIndexA;
            return true;
        }

        other = null;
        otherIndex = 0;
        return false;
    }
}
