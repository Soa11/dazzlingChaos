using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class RelayManager : MonoBehaviour
{
    public string LastJoinCode { get; private set; }

    private void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            Debug.LogError("RelayManager: NetworkManager.Singleton is null");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    public async void StartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("RelayManager: No NetworkManager found");
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("RelayManager: NetworkManager is already listening");
            return;
        }

        try
        {
            await InitUnity();

            Debug.Log("RelayManager: Creating Relay allocation...");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            LastJoinCode = joinCode;
            Debug.Log("RelayManager: JOIN CODE = " + joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            bool ok = NetworkManager.Singleton.StartHost();
            Debug.Log("RelayManager: StartHost result = " + ok);

            if (!ok)
            {
                Debug.LogError("RelayManager: Host failed to start");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("RelayManager: StartHost exception\n" + e);
        }
    }

    public async void StartClient(string joinCode)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("RelayManager: No NetworkManager found");
            return;
        }

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("RelayManager: Already hosting/server. Will not start client.");
            return;
        }

        if (NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("RelayManager: Already running as client");
            return;
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("RelayManager: Join code is empty");
            return;
        }

        joinCode = joinCode.Trim().ToUpper();

        try
        {
            await InitUnity();

            Debug.Log("RelayManager: Joining with code = " + joinCode);

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            bool ok = NetworkManager.Singleton.StartClient();
            Debug.Log("RelayManager: StartClient result = " + ok);

            if (!ok)
            {
                Debug.LogError("RelayManager: Client failed to start");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("RelayManager: StartClient exception\n" + e);
        }
    }

    private async Task InitUnity()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            Debug.Log("RelayManager: Initializing Unity Services...");
            await UnityServices.InitializeAsync();
            Debug.Log("RelayManager: Unity Services initialized");
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("RelayManager: Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("RelayManager: Signed in. PlayerId = " + AuthenticationService.Instance.PlayerId);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return;

        Debug.Log(
            "RelayManager: OnClientConnected -> clientId = " + clientId +
            " | localClientId = " + NetworkManager.Singleton.LocalClientId +
            " | IsHost = " + NetworkManager.Singleton.IsHost +
            " | IsServer = " + NetworkManager.Singleton.IsServer +
            " | IsClient = " + NetworkManager.Singleton.IsClient
        );
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("RelayManager: OnClientDisconnected -> clientId = " + clientId);
            return;
        }

        Debug.Log(
            "RelayManager: OnClientDisconnected -> clientId = " + clientId +
            " | localClientId = " + NetworkManager.Singleton.LocalClientId +
            " | IsHost = " + NetworkManager.Singleton.IsHost +
            " | IsServer = " + NetworkManager.Singleton.IsServer +
            " | IsClient = " + NetworkManager.Singleton.IsClient
        );
    }
}