using UnityEngine;

public class PlayerVerticalStateDetector : MonoBehaviour
{
    public enum VerticalState
    {
        Flat,
        Up,
        Down
    }

    [Header("References")]
    public Transform player;

    [Header("Tuning")]
    [Tooltip("How much Y change counts as real movement instead of tiny jitter.")]
    public float threshold = 0.002f;

    [Tooltip("Higher = smoother, lower = more immediate.")]
    public float smoothing = 6f;

    [Header("Debug")]
    public VerticalState currentState = VerticalState.Flat;
    public float rawDeltaY = 0f;
    public float smoothedDeltaY = 0f;

    private float previousY;
    private VerticalState previousState;

    void Start()
    {
        if (player != null)
            previousY = player.position.y;

        previousState = currentState;
    }

    void Update()
    {
        if (player == null)
            return;

        float currentY = player.position.y;
        rawDeltaY = currentY - previousY;

        smoothedDeltaY = Mathf.Lerp(
            smoothedDeltaY,
            rawDeltaY,
            smoothing * Time.deltaTime
        );

        if (smoothedDeltaY > threshold)
            currentState = VerticalState.Up;
        else if (smoothedDeltaY < -threshold)
            currentState = VerticalState.Down;
        else
            currentState = VerticalState.Flat;

        if (currentState != previousState)
        {
            Debug.Log($"Vertical State Changed -> {currentState}");
            previousState = currentState;
        }

        previousY = currentY;
    }

    public bool IsUp => currentState == VerticalState.Up;
    public bool IsDown => currentState == VerticalState.Down;
    public bool IsFlat => currentState == VerticalState.Flat;
}
