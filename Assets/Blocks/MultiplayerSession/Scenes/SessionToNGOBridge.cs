using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class SessionToNGOBridge : MonoBehaviour
{
    [SerializeField] private UIDocument joinCodeDocument;
    [SerializeField] private bool startAsHost;
    [SerializeField] private bool startAsClient;

    private bool ngoStarted = false;
    private float startupDelay = 0f;

    void Update()
    {
        if (ngoStarted) return;
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("Bridge: NetworkManager.Singleton is null");
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Bridge: NGO already listening");
            return;
        }

        if (joinCodeDocument == null)
        {
            Debug.Log("Bridge: UIDocument is null");
            return;
        }

        startupDelay += Time.deltaTime;
        if (startupDelay < 1.0f) return;

        var root = joinCodeDocument.rootVisualElement;
        if (root == null)
        {
            Debug.Log("Bridge: rootVisualElement is null");
            return;
        }

        string uiText = CollectAllLabelText(root);
        Debug.Log("Bridge UI text: " + uiText);

        if (string.IsNullOrEmpty(uiText)) return;
        if (uiText.Contains("No Session joined")) return;

        if (startAsHost)
        {
            Debug.Log("Bridge: Session detected -> StartHost");
            NetworkManager.Singleton.StartHost();
            ngoStarted = true;
        }
        else if (startAsClient)
        {
            Debug.Log("Bridge: Session detected -> StartClient");
            NetworkManager.Singleton.StartClient();
            ngoStarted = true;
        }
        else
        {
            Debug.Log("Bridge: Neither host nor client selected");
        }
    }

    private string CollectAllLabelText(VisualElement root)
    {
        var sb = new StringBuilder();

        foreach (var label in root.Query<Label>().ToList())
        {
            if (!string.IsNullOrEmpty(label.text))
            {
                sb.Append(label.text).Append(" ");
            }
        }

        foreach (var textField in root.Query<TextField>().ToList())
        {
            if (!string.IsNullOrEmpty(textField.value))
            {
                sb.Append(textField.value).Append(" ");
            }
        }

        foreach (var button in root.Query<Button>().ToList())
        {
            if (!string.IsNullOrEmpty(button.text))
            {
                sb.Append(button.text).Append(" ");
            }
        }

        return sb.ToString();
    }
}