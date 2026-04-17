using UnityEngine;
using Unity.Netcode;

public class SpectatorFollow : MonoBehaviour
{
    [Header("Switching")]
    public float switchInterval = 10f;

    [Header("Spectator Camera Offset")]
    public Vector3 spectatorOffset = new Vector3(-0.1f, 0.15f, -3.5f);
    public Vector3 spectatorEulerOffset = Vector3.zero;

    private PlayerCameraMarker[] cams;
    private Transform targetCam;
    private int currentIndex = -1;
    private float timer;

    void Update()
    {
        RefreshCameraList();

        if (cams == null || cams.Length == 0)
            return;

        timer += Time.deltaTime;

        if (targetCam == null || timer >= switchInterval)
        {
            timer = 0f;
            PickNextCamera();
        }

        if (targetCam != null)
        {
            FollowSelectedCamera();
        }
    }

    void RefreshCameraList()
    {
        cams = FindObjectsByType<PlayerCameraMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
    }

    void PickNextCamera()
    {
        if (cams == null || cams.Length == 0)
            return;

        currentIndex++;
        if (currentIndex >= cams.Length)
            currentIndex = 0;

        targetCam = cams[currentIndex].transform;

        Debug.Log(
            "[Spectator] index=" + currentIndex +
            " total=" + cams.Length +
            " name=" + targetCam.name +
            " active=" + targetCam.gameObject.activeInHierarchy +
            " id=" + targetCam.gameObject.GetInstanceID() +
            " worldPos=" + targetCam.position +
            " localPos=" + targetCam.localPosition
        );
    }

    void FollowSelectedCamera()
    {
        if (targetCam == null) return;

        Transform playerRoot = targetCam;

        while (playerRoot != null && playerRoot.GetComponent<NetworkObject>() == null)
        {
            playerRoot = playerRoot.parent;
        }

        if (playerRoot == null)
        {
            Debug.LogWarning("[Spectator] No NetworkObject parent found for targetCam.");
            return;
        }

        transform.position = playerRoot.TransformPoint(spectatorOffset);
        transform.rotation = playerRoot.rotation * Quaternion.Euler(spectatorEulerOffset);
    }
}