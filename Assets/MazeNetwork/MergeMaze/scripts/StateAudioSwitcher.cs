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

    private enum AudioState
    {
        Flat,
        Up,
        Down
    }

    private AudioState currentAudioState;
    private float lastSwitchTime = -999f;
    private bool initialized = false;
    private bool isSwitching = false;
    private Coroutine switchRoutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isLocalAudioPlayer = IsOwner;

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f; // local player test = 2D
        }

        // Only the owning local player should run this audio system
        if (!IsOwner)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.enabled = false;
            }

            enabled = false;
            return;
        }

        InitializeAudio();
    }

    void Start()
    {
        // In NGO, spawned prefab setup should happen in OnNetworkSpawn.
        // Keep Start empty to avoid initializing before ownership is known.
    }

    void InitializeAudio()
    {
        Debug.Log("[StateAudioSwitcher] InitializeAudio called");

        if (stateDetector == null)
        {
            Debug.LogError("[StateAudioSwitcher] stateDetector is missing");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("[StateAudioSwitcher] audioSource is missing");
            return;
        }

        if (flatClip == null)
        {
            Debug.LogError("[StateAudioSwitcher] flatClip is missing");
            return;
        }

        if (upClip == null)
        {
            Debug.LogError("[StateAudioSwitcher] upClip is missing");
            return;
        }

        if (downClip == null)
        {
            Debug.LogError("[StateAudioSwitcher] downClip is missing");
            return;
        }

        audioSource.enabled = true;
        audioSource.volume = targetVolume;

        if (stateDetector.IsUp)
        {
            currentAudioState = AudioState.Up;
            PlayImmediate(upClip);
        }
        else if (stateDetector.IsDown)
        {
            currentAudioState = AudioState.Down;
            PlayImmediate(downClip);
        }
        else
        {
            currentAudioState = AudioState.Flat;
            PlayImmediate(flatClip);
        }

        currentAudioStateName = currentAudioState.ToString();
        lastSwitchTime = Time.time;
        initialized = true;

        Debug.Log("[StateAudioSwitcher] Initialized successfully for owner");
    }

    void Update()
    {
        if (!initialized || isSwitching)
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

        AudioClip targetClip = flatClip;

        switch (currentAudioState)
        {
            case AudioState.Flat:
                targetClip = flatClip;
                Debug.Log("[StateAudioSwitcher] Switching to FLAT");
                break;

            case AudioState.Up:
                targetClip = upClip;
                Debug.Log("[StateAudioSwitcher] Switching to UP");
                break;

            case AudioState.Down:
                targetClip = downClip;
                Debug.Log("[StateAudioSwitcher] Switching to DOWN");
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