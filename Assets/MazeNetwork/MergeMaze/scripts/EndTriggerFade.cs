using UnityEngine;
using Unity.Netcode;

public class EndTriggerFade : MonoBehaviour
{
    public FadeInImage fadeImage;

    private static bool localMessageAlreadyShown = false;

    private void OnTriggerEnter(Collider other)
    {
        if (localMessageAlreadyShown)
            return;

        NetworkObject netObj = other.GetComponent<NetworkObject>();

        if (netObj != null && netObj.IsOwner)
        {
            localMessageAlreadyShown = true;
            fadeImage.FadeIn();
        }
    }

    public static void ResetMessageTrigger()
    {
        localMessageAlreadyShown = false;
    }
}