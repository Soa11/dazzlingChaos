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
    public PlayerSlopeReader slopeReader;

    [Header("Enter Thresholds From Flat")]
    [Tooltip("Must go above this to enter Up from Flat.")]
    public float enterUpThreshold = 0.4f;

    [Tooltip("Must go below this to enter Down from Flat.")]
    public float enterDownThreshold = -0.4f;

    [Header("Direct Up/Down Switch Thresholds")]
    [Tooltip("When currently Up, switch directly to Down if slopeY goes below this.")]
    public float upToDownThreshold = -0.4f;

    [Tooltip("When currently Down, switch directly to Up if slopeY goes above this.")]
    public float downToUpThreshold = 0.4f;

    [Header("Flat Detection")]
    [Tooltip("Slope must stay within +/- this value before we allow Flat.")]
    public float flatZoneThreshold = 0.12f;

    [Tooltip("How long slope must remain in flat zone before entering Flat.")]
    public float flatHoldTime = 0.15f;

    [Header("Debug")]
    public VerticalState currentVerticalState = VerticalState.Flat;
    public float currentSlopeY = 0f;

    private VerticalState previousVerticalState;
    private float flatTimer = 0f;

    void Start()
    {
        previousVerticalState = currentVerticalState;
    }

    void Update()
    {
        if (slopeReader == null)
            return;

        currentSlopeY = slopeReader.currentSlopeY;

        switch (currentVerticalState)
        {
            case VerticalState.Flat:
                flatTimer = 0f;

                if (currentSlopeY > enterUpThreshold)
                {
                    currentVerticalState = VerticalState.Up;
                }
                else if (currentSlopeY < enterDownThreshold)
                {
                    currentVerticalState = VerticalState.Down;
                }
                break;

            case VerticalState.Up:
                // Direct Up -> Down
                if (currentSlopeY < upToDownThreshold)
                {
                    flatTimer = 0f;
                    currentVerticalState = VerticalState.Down;
                }
                // Only allow Flat if slope stays near zero briefly
                else if (Mathf.Abs(currentSlopeY) <= flatZoneThreshold)
                {
                    flatTimer += Time.deltaTime;

                    if (flatTimer >= flatHoldTime)
                    {
                        currentVerticalState = VerticalState.Flat;
                        flatTimer = 0f;
                    }
                }
                else
                {
                    flatTimer = 0f;
                }
                break;

            case VerticalState.Down:
                // Direct Down -> Up
                if (currentSlopeY > downToUpThreshold)
                {
                    flatTimer = 0f;
                    currentVerticalState = VerticalState.Up;
                }
                // Only allow Flat if slope stays near zero briefly
                else if (Mathf.Abs(currentSlopeY) <= flatZoneThreshold)
                {
                    flatTimer += Time.deltaTime;

                    if (flatTimer >= flatHoldTime)
                    {
                        currentVerticalState = VerticalState.Flat;
                        flatTimer = 0f;
                    }
                }
                else
                {
                    flatTimer = 0f;
                }
                break;
        }

        if (currentVerticalState != previousVerticalState)
        {
            Debug.Log($"Vertical State Changed -> {currentVerticalState} | slopeY = {currentSlopeY:F4}");
            previousVerticalState = currentVerticalState;
        }
    }

    // AudioSwitcher-friendly bool PROPERTIES
    public bool IsUp => currentVerticalState == VerticalState.Up;
    public bool IsDown => currentVerticalState == VerticalState.Down;
    public bool IsFlat => currentVerticalState == VerticalState.Flat;
}