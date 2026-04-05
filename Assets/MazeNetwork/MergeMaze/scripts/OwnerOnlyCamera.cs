using Unity.Netcode;
using UnityEngine;

public class OwnerOnlyCamera : NetworkBehaviour
{
    [SerializeField] private GameObject cameraObject;

    private void Awake()
    {
        if (cameraObject == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
                cameraObject = cam.gameObject;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (cameraObject != null)
            cameraObject.SetActive(IsOwner);
    }
}
