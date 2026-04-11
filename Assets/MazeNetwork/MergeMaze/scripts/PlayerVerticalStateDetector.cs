using UnityEngine;

public class PlayerVerticalStateDetector : MonoBehaviour
{
    public enum VerticalState
    {
        Up,
        Down
    }

    [Header("References")]
    public Transform player;

    [Header("Averaging Window")]
    [Tooltip("How long to average Y values before comparing to the previous average.")]
    public float averageWindowSeconds = 0.25f;

    [Tooltip("Minimum average Y difference needed to change state.")]
    public float switchThreshold = 0.005f;

    [Header("Debug")]
    public VerticalState currentVerticalState = VerticalState.Down;

    public float currentAverageY = 0f;
    public float previousAverageY = 0f;
    public float averageDifference = 0f;

    private float timer = 0f;
    private float ySum = 0f;
    private int sampleCount = 0;

    private VerticalState previousVerticalState;
    private bool hasPreviousAverage = false;

    void Start()
    {
        previousVerticalState = currentVerticalState;

        if (player != null)
        {
            currentAverageY = player.position.y;
            previousAverageY = player.position.y;
        }
    }

    void Update()
    {
        if (player == null)
            return;

        // Collect samples during the window
        ySum += player.position.y;
        sampleCount++;
        timer += Time.deltaTime;

        if (timer >= averageWindowSeconds && sampleCount > 0)
        {
            currentAverageY = ySum / sampleCount;

            if (hasPreviousAverage)
            {
                averageDifference = currentAverageY - previousAverageY;

                if (averageDifference > switchThreshold)
                {
                    currentVerticalState = VerticalState.Up;
                }
                else if (averageDifference < -switchThreshold)
                {
                    currentVerticalState = VerticalState.Down;
                }
                // If within threshold, keep previous state

                if (currentVerticalState != previousVerticalState)
                {
                    Debug.Log(
                        $"Vertical State Changed -> {currentVerticalState} " +
                        $"(prevAvgY: {previousAverageY:F4}, currentAvgY: {currentAverageY:F4}, diff: {averageDifference:F4})"
                    );

                    previousVerticalState = currentVerticalState;
                }
            }

            previousAverageY = currentAverageY;
            hasPreviousAverage = true;

            // Reset window
            timer = 0f;
            ySum = 0f;
            sampleCount = 0;
        }
    }

    public bool IsUp => currentVerticalState == VerticalState.Up;
    public bool IsDown => currentVerticalState == VerticalState.Down;
}