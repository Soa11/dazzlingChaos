using TMPro;
using UnityEngine;

public class PlayerNameSync : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;

    void Update()
    {
        if (nameText == null) return;
        nameText.text = LocalPlayerName.Value;
    }
}