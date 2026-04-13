using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class StateAudioSwitcher : NetworkBehaviour
{
    [Header("References")]
    public PlayerVerticalStateDetector stateDetector;
    public AudioSource audioSource;

    [Header("Clips")]
    public AudioClip flatClip;
    public AudioClip upClip;
    public AudioClip downClip;

    [Header("Timing")]
    public float minHoldTime = 0.30f;
    public float fadeOutTime = 0.03f;
    public float fadeInTime = 0.06f;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float targetVolume = 0.6f;

    [Header("Debug")]
    public string currentClipName = "";
    public string currentAudioStateName = "";
    public bool isLocalAudioPlayer = false;
    public bool initialized = false;

    private enum AudioState
    {
        Flat,
        Up,
        Down
    }

    private AudioState currentAudioState = AudioState.Flat;
    private float lastSwitchTime = -999f;
    private bool isSwitching = false;
    private Coroutine switchRoutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            isLocalAudioPlayer = false;

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.enabled = false;
            }

            enabled = false;
            return;
        }

        isLocalAudioPlayer = true;

        if (audioSource != null)
        {
            audioSource.enabled = true;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = targetVolume;
        }

        StartCoroutine(WaitAndInitialize());
    }

    private IEnumerator WaitAndInitialize()
    {
        yield return null;

        if (stateDetector == null)
        {
            Debug.LogError("[StateAudioSwitcher] stateDetector is missing");
            yield break;
        }

        if (audioSource == null)
        {
            Debug.LogError("[StateAudioSwitcher] audioSource is missing");
            yield break;
        }

        if (flatClip == null || upClip == null || downClip == null)
        {
            Debug.LogError("[StateAudioSwitcher] One or more clips are missing");
            yield break;
        }

        AudioClip initialClip = flatClip;
        currentAudioState = AudioState.Flat;

        if (stateDetector.IsUp)
        {
            initialClip = upClip;
            currentAudioState = AudioState.Up;
        }
        else if (stateDetector.IsDown)
        {
            initialClip = downClip;
            currentAudioState = AudioState.Down;
        }

        PlayImmediate(initialClip);

        currentAudioStateName = currentAudioState.ToString();
        lastSwitchTime = Time.time;
        initialized = true;

        Debug.Log("[StateAudioSwitcher] Initialized successfully for local owner");
    }

    void Update()
    {
        if (!IsOwner || !initialized || isSwitching)
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

        PerformStateSwitch(wantedState);
    }

    private void PerformStateSwitch(AudioState newState)
    {
        currentAudioState = newState;
        currentAudioStateName = newState.ToString();
        lastSwitchTime = Time.time;

        AudioClip targetClip = flatClip;

        switch (newState)
        {
            case AudioState.Up:
                targetClip = upClip;
                Debug.Log("[StateAudioSwitcher] Switching to UP");
                break;

            case AudioState.Down:
                targetClip = downClip;
                Debug.Log("[StateAudioSwitcher] Switching to DOWN");
                break;

            default:
                targetClip = flatClip;
                Debug.Log("[StateAudioSwitcher] Switching to FLAT");
                break;
        }

        if (switchRoutine != null)
            StopCoroutine(switchRoutine);

        switchRoutine = StartCoroutine(SwitchWithFade(targetClip));
    }

    void PlayImmediate(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.volume = targetVolume;
        audioSource.Play();

        currentClipName = clip.name;
        Debug.Log("[StateAudioSwitcher] Playing initial clip: " + clip.name);
    }

    IEnumerator SwitchWithFade(AudioClip newClip)
    {
        if (newClip == null || audioSource == null)
            yield break;

        if (audioSource.clip == newClip && audioSource.isPlaying)
            yield break;

        isSwitching = true;

        float startVolume = audioSource.volume;

        float t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeOutTime);
            audioSource.volume = Mathf.Lerp(startVolume, 0f, k);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        audioSource.clip = newClip;
        audioSource.loop = true;
        audioSource.Play();

        currentClipName = newClip.name;
        Debug.Log("[StateAudioSwitcher] New clip started: " + newClip.name);

        t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeInTime);
            audioSource.volume = Mathf.Lerp(0f, targetVolume, k);
            yield return null;
        }

        audioSource.volume = targetVolume;
        isSwitching = false;
        switchRoutine = null;
    }

    public override void OnNetworkDespawn()
    {
        if (audioSource != null)
            audioSource.Stop();

        initialized = false;
        isSwitching = false;
        switchRoutine = null;

        base.OnNetworkDespawn();
    }
}