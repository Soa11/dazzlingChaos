using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private GameObject hostClientUI;
    [SerializeField] private GameObject joinByCodeUI;

    private void Start()
    {
        if (hostClientUI != null) hostClientUI.SetActive(true);
        if (joinByCodeUI != null) joinByCodeUI.SetActive(false);
    }

    public void StartHostGame()
    {
        Debug.Log("HOST button pressed");
        if (hostClientUI != null) hostClientUI.SetActive(false);
        if (joinByCodeUI != null) joinByCodeUI.SetActive(true);
    }

    public void StartClientGame()
    {
        Debug.Log("CLIENT button pressed");
        if (hostClientUI != null) hostClientUI.SetActive(false);
        if (joinByCodeUI != null) joinByCodeUI.SetActive(true);
    }
}