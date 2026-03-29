using UnityEngine;
using UnityEngine.UI;

public class NameFromUI : MonoBehaviour
{
    [SerializeField] private InputField nameInput;

    public void SaveTypedName()
    {
        if (nameInput == null)
        {
            Debug.Log("nameInput is NULL");
            return;
        }

        string typedName = nameInput.text.Trim();

        Debug.Log("Typed name: " + typedName);

        if (!string.IsNullOrEmpty(typedName))
        {
            LocalPlayerName.Value = typedName;
            Debug.Log("Saved name: " + LocalPlayerName.Value);
        }
    }
}