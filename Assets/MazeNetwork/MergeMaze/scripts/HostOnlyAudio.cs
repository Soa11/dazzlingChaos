using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(AudioSource))]
public class HostOnlyAudio : NetworkBehaviour
{
    [Header("Audio")]
    public AudioClip hostClip;
    [Range(0f, 1f)] public float volume = 0.6f;
    public bool loop = true;
    public bool playOnNetworkSpawn = true;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.spatialBlend = 0f; // 2D
        audioSource.volume = volume;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the Host machine should play this audio
        if (!IsHost)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.enabled = false;
            }

            return;
        }

        audioSource.enabled = true;
        audioSource.loop = loop;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;

        if (hostClip != null)
        {
            audioSource.clip = hostClip;

            if (playOnNetworkSpawn)
                audioSource.Play();
        }
        else
        {
            //Debug.LogWarning("[HostOnlyAudio] No hostClip assigned.");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (audioSource != null)
            audioSource.Stop();

        base.OnNetworkDespawn();
    }

    public void PlayHostAudio()
    {
        if (!IsHost || audioSource == null || hostClip == null)
            return;

        audioSource.clip = hostClip;
        audioSource.Play();
    }

    public void StopHostAudio()
    {
        if (audioSource == null)
            return;

        audioSource.Stop();
    }
}