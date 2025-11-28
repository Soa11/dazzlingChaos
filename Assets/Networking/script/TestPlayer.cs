using UnityEngine;
using Mirror;

public class TestPlayer : NetworkBehaviour
{
    public float speed = 5f;

    void Update()
    {
        // Only move our own player
        if (!isLocalPlayer) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 dir = new Vector3(h, 0, v);
        transform.position += dir * speed * Time.deltaTime;
    }
}
