using UnityEngine;

public class MapMarkerFollower : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Map Settings")]
    public float mapY = 109f;
    public Vector3 offset = Vector3.zero;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 p = target.position;
        transform.position = new Vector3(
            p.x + offset.x,
            mapY + offset.y,
            p.z + offset.z
        );
    }
}