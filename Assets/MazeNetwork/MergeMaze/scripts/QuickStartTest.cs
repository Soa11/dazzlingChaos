using Unity.Netcode;
using UnityEngine;

public class QuickStartTest : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (!NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("Started Host");
            }
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            if (!NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("Started Client");
            }
        }
    }
}