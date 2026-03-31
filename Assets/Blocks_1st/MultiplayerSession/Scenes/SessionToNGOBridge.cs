using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class SessionToNGOBridge : MonoBehaviour
{
    [SerializeField] private UIDocument joinCodeDocument;

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

        bool isEditor = Application.isEditor;
        bool shouldStartHost = isEditor;      // Editor = Host
        bool shouldStartClient = !isEditor;   // Build = Client

        if (shouldStartHost)
        {
            Debug.Log("Bridge: StartHost");
            NetworkManager.Singleton.StartHost();
            ngoStarted = true;
        }
        else if (shouldStartClient)
        {
            Debug.Log("Bridge: StartClient");
            NetworkManager.Singleton.StartClient();
            ngoStarted = true;
        }
    }

    private string CollectAllLabelText(VisualElement root)
    {
        var sb = new StringBuilder();

        foreach (var label in root.Query<Label>().ToList())
        {
            if (!string.IsNullOrEmpty(label.text))
            {
                sb.Append(label.text);
                sb.Append(" ");
            }
        }

        foreach (var textField in root.Query<TextField>().ToList())
        {
            if (!string.IsNullOrEmpty(textField.value))
            {
                sb.Append(textField.value);
                sb.Append(" ");
            }
        }

        foreach (var button in root.Query<Button>().ToList())
        {
            if (!string.IsNullOrEmpty(button.text))
            {
                sb.Append(button.text);
                sb.Append(" ");
            }
        }

        return sb.ToString();
    }
}