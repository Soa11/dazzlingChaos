using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class HideMenuOnSpawn : NetworkBehaviour
{
    [SerializeField] private float hideDelay = 2f;
    [SerializeField] private UIDocument menuDocument;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        StartCoroutine(HideMenuAfterDelay());
    }

    private IEnumerator HideMenuAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);

        if (menuDocument == null)
        {
            menuDocument = FindFirstObjectByType<UIDocument>();
        }

        if (menuDocument != null)
        {
            menuDocument.rootVisualElement.style.display = DisplayStyle.None;
        }
        else
        {
            Debug.LogWarning("HideMenuOnSpawn: UIDocument not found.");
        }
    }
}