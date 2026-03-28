using TMPro;
using UnityEngine;

public class PlayerNameSync : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;

    public string playerName = "Player";

    private string lastShownName = "";

    void Start()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (playerName == "Player")
            playerName = "Desktop";
#elif UNITY_ANDROID
        if (playerName == "Player")
            playerName = "Android";
#elif UNITY_IOS
        if (playerName == "Player")
            playerName = "iPhone";
#endif

        RefreshName();
    }

    void Update()
    {
        if (playerName != lastShownName)
            RefreshName();
    }

    void RefreshName()
    {
        lastShownName = playerName;

        if (nameText != null)
            nameText.text = playerName;
    }
}