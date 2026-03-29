using UnityEngine;
using UnityEngine.UI;

public class CoherenceNameDebug : MonoBehaviour
{
    [SerializeField] private InputField nameInput;

    void Update()
    {
        if (nameInput == null)
            return;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Typed in Coherence name field: [" + nameInput.text + "]");
        }
    }

    public void PrintCurrentName()
    {
        if (nameInput == null)
        {
            Debug.LogWarning("nameInput is NULL");
            return;
        }

        Debug.Log("Typed in Coherence name field: [" + nameInput.text + "]");
    }
}