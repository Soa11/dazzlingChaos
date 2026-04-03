using TMPro;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
using Unity.Services.Authentication;

public class PlayerNameDisplay : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;

    private readonly NetworkVariable<FixedString64Bytes> networkPlayerName =
        new NetworkVariable<FixedString64Bytes>("Player");

    public override void OnNetworkSpawn()
    {
        networkPlayerName.OnValueChanged += OnPlayerNameChanged;
        OnPlayerNameChanged(default, networkPlayerName.Value);

        if (IsOwner)
        {
            string authName = AuthenticationService.Instance.PlayerName;

            if (string.IsNullOrWhiteSpace(authName))
                authName = "Player";

            string cleanName = StripSuffix(authName);

            SubmitPlayerNameServerRpc(cleanName);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkPlayerName.OnValueChanged -= OnPlayerNameChanged;
    }

    [ServerRpc]
    private void SubmitPlayerNameServerRpc(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            newName = "Player";

        networkPlayerName.Value = newName;
    }

    private void OnPlayerNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        if (playerNameText != null)
            playerNameText.text = newValue.ToString();
    }

    private string StripSuffix(string fullName)
    {
        int hashIndex = fullName.IndexOf('#');
        if (hashIndex > 0)
            return fullName.Substring(0, hashIndex);

        return fullName;
    }
}