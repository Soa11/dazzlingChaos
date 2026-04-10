using UnityEngine;

public class GreenSoundPitchTest : MonoBehaviour
{
    public Transform player;
    public AudioSource audioSource;

    [Header("Pitch")]
    public float basePitch = 1f;
    public float upPitch = 1.01f;
    public float downPitch = 0.99f;
    public float pitchSmoothSpeed = 1.5f;

    [Header("Y Detection")]
    public float motionThreshold = 0.0005f;

    private float previousY;

    void Start()
    {
        if (player != null)
            previousY = player.position.y;
    }

    void Update()
    {
        if (player == null || audioSource == null)
            return;

        float currentY = player.position.y;
        float deltaY = currentY - previousY;

        float targetPitch = basePitch;

        if (deltaY > motionThreshold)
            targetPitch = upPitch;
        else if (deltaY < -motionThreshold)
            targetPitch = downPitch;

        audioSource.pitch = Mathf.Lerp(
            audioSource.pitch,
            targetPitch,
            pitchSmoothSpeed * Time.deltaTime
        );

        previousY = currentY;
    }
}