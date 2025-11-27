using UnityEngine;
using Coherence.Toolkit;   // coherence namespace

[RequireComponent(typeof(CoherenceSync))]
public class NetCubeController : MonoBehaviour
{
    private CoherenceSync _sync;

    public float moveSpeed = 5f;

    void Awake()
    {
        _sync = GetComponent<CoherenceSync>();
    }

    void Update()
    {
        // Only the instance that has State Authority is allowed to move the cube
        if (!_sync.HasStateAuthority)
            return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
        {
            Vector3 dir = new Vector3(h, 0f, v);
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
    }
}
