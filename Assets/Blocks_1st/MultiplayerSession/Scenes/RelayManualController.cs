using UnityEngine;

public class RelayManualController : MonoBehaviour
{
    [SerializeField] private GameObject hostClientUI; // Your buttons
    [SerializeField] private GameObject joinByCodeUI; // The Building Block

    private void Start()
    {
        // Setup initial view
        if (hostClientUI != null) hostClientUI.SetActive(true);
        if (joinByCodeUI != null) joinByCodeUI.SetActive(false);
    }

    public void StartHostGame()
    {
        Debug.Log("Switching to Building Block UI to Host...");
        // Hide your buttons
        if (hostClientUI != null) hostClientUI.SetActive(false);
        // Show the Building Block
        if (joinByCodeUI != null) joinByCodeUI.SetActive(true);

        // IMPORTANT: We let the Building Block call StartHost() 
        // internally when you click 'CREATE' in its menu.
    }

    public void StartClientGame()
    {
        Debug.Log("Switching to Building Block UI to Join...");
        if (hostClientUI != null) hostClientUI.SetActive(false);
        if (joinByCodeUI != null) joinByCodeUI.SetActive(true);

        // IMPORTANT: We let the Building Block call StartClient() 
        // internally when you click 'JOIN' in its menu.
    }
}