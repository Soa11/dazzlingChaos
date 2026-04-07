using System.Collections;
using UnityEngine;

public class FadeInImage : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public float fadeDuration = 3f;

    private Coroutine fadeRoutine;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 0f; // start invisible
    }

    void Start()
    {
        
    }

    public void FadeIn()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            float t = time / fadeDuration;

            // smooth ease-in (feels more natural)
            t = t * t;

            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        canvasGroup.alpha = 1f;
    }
}