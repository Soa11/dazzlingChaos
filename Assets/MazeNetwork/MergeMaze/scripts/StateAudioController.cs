using UnityEngine;

public class StateAudioController : MonoBehaviour
{
    [Header("References")]
    public PlayerVerticalStateDetector stateDetector;

    public AudioSource upSource;
    public AudioSource downSource;

    [Header("Fade")]
    [Tooltip("How quickly volumes move toward their targets.")]
    public float fadeSpeed = 4f;

    [Header("Target Volumes")]
    public float upTargetVolume = 1f;
    public float downTargetVolume = 1f;

    void Update()
    {
        if (stateDetector == null) return;
        if (upSource == null || downSource == null) return;

        float upTarget = 0f;
        float downTarget = 0f;

        if (stateDetector.IsUp)
        {
            upTarget = upTargetVolume;
        }
        else if (stateDetector.IsDown)
        {
            downTarget = downTargetVolume;
        }

        upSource.volume = Mathf.MoveTowards(
            upSource.volume,
            upTarget,
            fadeSpeed * Time.deltaTime
        );

        downSource.volume = Mathf.MoveTowards(
            downSource.volume,
            downTarget,
            fadeSpeed * Time.deltaTime
        );
    }
}