using UnityEngine;

public class StateAudioSwitcher : MonoBehaviour
{
    [Header("References")]
    public PlayerVerticalStateDetector stateDetector;
    public AudioSource audioSource;

    [Header("Clips")]
    public AudioClip flatClip;
    public AudioClip upClip;
    public AudioClip downClip;

    [Header("Timing")]
    [Tooltip("Minimum time the current sound should stay before switching.")]
    public float minHoldTime = 0.7f;

    [Header("Debug")]
    public string currentClipName = "";
    public string currentAudioStateName = "";

    private enum AudioState
    {
        Flat,
        Up,
        Down
    }

    private AudioState currentAudioState;
    private float lastSwitchTime = -999f;
    private bool initialized = false;

    void Start()
    {
        if (stateDetector == null || audioSource == null || flatClip == null || upClip == null || downClip == null)
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
        else if (stateDetector.IsDown)
        {
            currentAudioState = AudioState.Down;
            PlayClip(downClip);
        }
        else
        {
            currentAudioState = AudioState.Flat;
            PlayClip(flatClip);
        }

        currentAudioStateName = currentAudioState.ToString();
        lastSwitchTime = Time.time;
        initialized = true;
    }

    void Update()
    {
        if (!initialized)
            return;

        AudioState wantedState = currentAudioState;

        if (stateDetector.IsUp)
            wantedState = AudioState.Up;
        else if (stateDetector.IsDown)
            wantedState = AudioState.Down;
        else if (stateDetector.IsFlat)
            wantedState = AudioState.Flat;

        if (wantedState == currentAudioState)
            return;

        if (Time.time < lastSwitchTime + minHoldTime)
            return;

        currentAudioState = wantedState;
        currentAudioStateName = currentAudioState.ToString();
        lastSwitchTime = Time.time;

        switch (currentAudioState)
        {
            case AudioState.Flat:
                PlayClip(flatClip);
                Debug.Log("[StateAudioSwitcher] Switched to FLAT clip");
                break;

            case AudioState.Up:
                PlayClip(upClip);
                Debug.Log("[StateAudioSwitcher] Switched to UP clip");
                break;

            case AudioState.Down:
                PlayClip(downClip);
                Debug.Log("[StateAudioSwitcher] Switched to DOWN clip");
                break;
        }
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();

        currentClipName = clip.name;
    }
}