using UnityEngine;
using UnityEngine.UI;

public class CoherenceNameBridge : MonoBehaviour
{
    [SerializeField] private InputField nameInput;

    void Start()
    {
        if (nameInput != null)
        {
            nameInput.onEndEdit.AddListener(OnNameEntered);
        }
    }

    void OnNameEntered(string value)
    {
        value = value.Trim();

        if (!string.IsNullOrEmpty(value))
        {
            LocalPlayerName.Value = value;
            Debug.Log("Name set to: " + value);
        }
    }
}