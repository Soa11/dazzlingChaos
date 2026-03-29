using UnityEngine;

public class SimplePlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Mobile Tilt")]
    public float deadZone = 0.08f;
    public float tiltSensitivity = 6f;

    private CharacterController controller;
    private Vector3 velocity;

    private Vector3 baseAcceleration;
    private bool gyroReady = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
#if UNITY_IOS || UNITY_ANDROID
        Input.gyro.enabled = true;
        baseAcceleration = Input.acceleration;
        gyroReady = true;
#endif
    }

    void Update()
    {
        Vector3 move = Vector3.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        move = new Vector3(h, 0f, v);

        if (move.magnitude > 1f)
            move.Normalize();
#else
        if (gyroReady)
        {
            Vector3 currentAcceleration = Input.acceleration;
            Vector3 delta = currentAcceleration - baseAcceleration;

            float x = delta.x;
            float z = -delta.z;

            if (Mathf.Abs(x) < deadZone) x = 0f;
            if (Mathf.Abs(z) < deadZone) z = 0f;

            move = new Vector3(x, 0f, z) * tiltSensitivity;

            if (move.magnitude > 1f)
                move.Normalize();
        }
#endif

        controller.Move(move * moveSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void RecalibrateTilt()
    {
#if UNITY_IOS || UNITY_ANDROID
        baseAcceleration = Input.acceleration;
#endif
    }
}