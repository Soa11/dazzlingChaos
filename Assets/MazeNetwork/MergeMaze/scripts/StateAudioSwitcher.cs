using UnityEngine;

public class StateAudioSwitcher : MonoBehaviour
{
    [Header("References")]
    public PlayerVerticalStateDetector stateDetector;

    [Tooltip("Main AudioSource")]
    public AudioSource audioSourceA;

    [Tooltip("Second AudioSource for crossfade")]
    public AudioSource audioSourceB;

    [Header("Clips")]
    public AudioClip flatClip;
    public AudioClip upClip;
    public AudioClip downClip;

    [Header("Timing")]
    [Tooltip("Minimum time the current sound should stay before switching.")]
    public float minHoldTime = 0.25f;

    [Tooltip("How long to crossfade between clips.")]
    public float crossfadeDuration = 0.28f;

    [Header("Volumes")]
    [Range(0f, 1f)]
    public float targetVolume = 1f;

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

    private AudioSource activeSource;
    private AudioSource inactiveSource;

    private bool isCrossfading = false;
    private float crossfadeTimer = 0f;
    private float fadeOutStartVolume = 0f;
    private float fadeInTargetVolume = 1f;

    void Start()
    {
        if (stateDetector == null || audioSourceA == null || audioSourceB == null ||
            flatClip == null || upClip == null || downClip == null)
        {
            Debug.LogWarning("[StateAudioSwitcher] Missing reference.");
            enabled = false;
            return;
        }

        PrepareSource(audioSourceA);
        PrepareSource(audioSourceB);

        activeSource = audioSourceA;
        inactiveSource = audioSourceB;

        if (stateDetector.IsUp)
        {
            currentAudioState = AudioState.Up;
            PlayImmediate(activeSource, upClip, targetVolume);
        }
        else if (stateDetector.IsDown)
        {
            currentAudioState = AudioState.Down;
            PlayImmediate(activeSource, downClip, targetVolume);
        }
        else
        {
            currentAudioState = AudioState.Flat;
            PlayImmediate(activeSource, flatClip, targetVolume);
        }

        inactiveSource.volume = 0f;
        inactiveSource.Stop();

        currentAudioStateName = currentAudioState.ToString();
        lastSwitchTime = Time.time;
        initialized = true;
    }

    void Update()
    {
        if (!initialized)
            return;

        UpdateCrossfade();

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
                CrossfadeTo(flatClip);
                Debug.Log("[StateAudioSwitcher] Crossfading to FLAT clip");
                break;

            case AudioState.Up:
                CrossfadeTo(upClip);
                Debug.Log("[StateAudioSwitcher] Crossfading to UP clip");
                break;

            case AudioState.Down:
                CrossfadeTo(downClip);
                Debug.Log("[StateAudioSwitcher] Crossfading to DOWN clip");
                break;
        }
    }

    void PrepareSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
    }

    void PlayImmediate(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null)
            return;

        source.Stop();
        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.Play();

        currentClipName = clip.name;
    }

    void CrossfadeTo(AudioClip newClip)
    {
        if (newClip == null || inactiveSource == null || activeSource == null)
            return;

        // If active source is already playing this clip, do nothing
        if (activeSource.clip == newClip && activeSource.isPlaying)
            return;

        inactiveSource.Stop();
        inactiveSource.clip = newClip;
        inactiveSource.loop = true;
        inactiveSource.volume = 0f;
        inactiveSource.Play();

        fadeOutStartVolume = activeSource.volume;
        fadeInTargetVolume = targetVolume;

        crossfadeTimer = 0f;
        isCrossfading = true;

        currentClipName = newClip.name;
    }

    void UpdateCrossfade()
    {
        if (!isCrossfading)
            return;

        if (crossfadeDuration <= 0f)
        {
            FinishCrossfadeImmediate();
            return;
        }

        crossfadeTimer += Time.deltaTime;
        float t = Mathf.Clamp01(crossfadeTimer / crossfadeDuration);

        // SmoothStep makes the blend softer than linear
        t = t * t * (3f - 2f * t);

        activeSource.volume = Mathf.Lerp(fadeOutStartVolume, 0f, t);
        inactiveSource.volume = Mathf.Lerp(0f, fadeInTargetVolume, t);

        if (crossfadeTimer >= crossfadeDuration)
        {
            activeSource.Stop();
            activeSource.volume = 0f;

            AudioSource oldActive = activeSource;
            activeSource = inactiveSource;
            inactiveSource = oldActive;

            isCrossfading = false;
        }
    }

    void FinishCrossfadeImmediate()
    {
        activeSource.Stop();
        activeSource.volume = 0f;

        inactiveSource.volume = fadeInTargetVolume;

        AudioSource oldActive = activeSource;
        activeSource = inactiveSource;
        inactiveSource = oldActive;

        isCrossfading = false;
    }
}