using TMPro;
using UnityEngine;

public class MapNameSync : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Text")]
    public TextMeshPro nameText;

    void LateUpdate()
    {
        if (target == null || nameText == null)
            return;

        PlayerNameDisplay playerName = target.GetComponent<PlayerNameDisplay>();

        if (playerName != null)
        {
            nameText.text = playerName.CurrentPlayerName;
        }
    }
}