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

    [Header("Enter Thresholds")]
    [Tooltip("Must go above this to enter Up.")]
    public float enterUpThreshold = 0.12f;

    [Tooltip("Must go below this to enter Down.")]
    public float enterDownThreshold = -0.12f;

    [Header("Exit Thresholds")]
    [Tooltip("When currently Up, come back to Flat if slopeY drops below this.")]
    public float exitUpToFlatThreshold = 0.08f;

    [Tooltip("When currently Down, come back to Flat if slopeY rises above this.")]
    public float exitDownToFlatThreshold = -0.08f;

    [Header("Debug")]
    public VerticalState currentVerticalState = VerticalState.Flat;
    public float currentSlopeY = 0f;

    private VerticalState previousVerticalState;

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
                if (currentSlopeY > enterUpThreshold)
                    currentVerticalState = VerticalState.Up;
                else if (currentSlopeY < enterDownThreshold)
                    currentVerticalState = VerticalState.Down;
                break;

            case VerticalState.Up:
                if (currentSlopeY < exitUpToFlatThreshold)
                    currentVerticalState = VerticalState.Flat;
                break;

            case VerticalState.Down:
                if (currentSlopeY > exitDownToFlatThreshold)
                    currentVerticalState = VerticalState.Flat;
                break;
        }

        if (currentVerticalState != previousVerticalState)
        {
            Debug.Log($"Vertical State Changed -> {currentVerticalState} | slopeY = {currentSlopeY:F4}");
            previousVerticalState = currentVerticalState;
        }
    }

    public bool IsUp => currentVerticalState == VerticalState.Up;
    public bool IsDown => currentVerticalState == VerticalState.Down;
    public bool IsFlat => currentVerticalState == VerticalState.Flat;
}