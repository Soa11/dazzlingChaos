using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class HostOnlyCameraActivator : MonoBehaviour
{
    public GameObject spectatorCamObject;

    IEnumerator Start()
    {
        if (spectatorCamObject != null)
            spectatorCamObject.SetActive(false);

        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        bool isHost = NetworkManager.Singleton.IsHost;

        if (spectatorCamObject != null)
            spectatorCamObject.SetActive(isHost);
    }
}