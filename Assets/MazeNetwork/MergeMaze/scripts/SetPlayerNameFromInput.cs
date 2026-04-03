using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Authentication;

public class SetPlayerNameFromInput : MonoBehaviour
{
    public UIDocument uiDocument;

    void Start()
    {
        var root = uiDocument.rootVisualElement;

        var createSection = root.Q<VisualElement>("CreateSessionElement");
        var joinSection = root.Q<VisualElement>("JoinSessionByCode");

        var createButton = createSection?.Q<Button>();
        var joinButton = joinSection?.Q<Button>();

        if (createButton != null)
            createButton.clicked += SetName;

        if (joinButton != null)
            joinButton.clicked += SetName;
    }

    public async void SetName()
    {
        var root = uiDocument.rootVisualElement;
        var input = root.Q<TextField>("PlayerNameInput");

        if (input == null)
        {
            Debug.LogError("PlayerNameInput not found");
            return;
        }

        string playerName = input.value?.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("Player name is empty");
            return;
        }

        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
        Debug.Log("Player name set to: " + playerName);
    }
}