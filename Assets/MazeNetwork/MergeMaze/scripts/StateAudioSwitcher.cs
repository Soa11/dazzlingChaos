using UnityEngine;

public class StateAudioSwitcher : MonoBehaviour
{
    [Header("References")]
    public PlayerVerticalStateDetector stateDetector;
    public AudioSource audioSource;

    [Header("Clips")]
    public AudioClip upClip;
    public AudioClip downClip;

    [Header("Switch Timing")]
    [Tooltip("Minimum time before switching to another clip.")]
    public float switchCooldown = 1.5f;

    [Header("Debug")]
    public string currentClipName = "";

    private bool hasStarted = false;
    private float lastSwitchTime = -999f;

    private enum AudioState
    {
        Up,
        Down
    }

    private AudioState currentAudioState;

    void Start()
    {
        if (stateDetector == null || audioSource == null || upClip == null || downClip == null)
        {
            Debug.LogWarning("[StateAudioSwitcher] Missing reference.");
            enabled = false;
            return;
        }

        if (stateDetector.IsUp)
        {
            currentAudioState = AudioState.Up;
            PlayClip(upClip);
        }
        else
        {
            currentAudioState = AudioState.Down;
            PlayClip(downClip);
        }

        hasStarted = true;
        lastSwitchTime = Time.time;
    }

    void Update()
    {
        if (!hasStarted)
            return;

        if (Time.time < lastSwitchTime + switchCooldown)
            return;

        if (stateDetector.IsUp && currentAudioState != AudioState.Up)
        {
            currentAudioState = AudioState.Up;
            PlayClip(upClip);
            lastSwitchTime = Time.time;
            Debug.Log("[StateAudioSwitcher] Switched to UP clip");
        }
        else if (stateDetector.IsDown && currentAudioState != AudioState.Down)
        {
            currentAudioState = AudioState.Down;
            PlayClip(downClip);
            lastSwitchTime = Time.time;
            Debug.Log("[StateAudioSwitcher] Switched to DOWN clip");
        }
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();

        currentClipName = clip.name;
    }
}
