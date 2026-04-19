using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class TripleTapExit : NetworkBehaviour
{
    [Header("Triple Tap Settings")]
    public float tapWindow = 1.0f;
    public float enableDelayAfterSpawn = 2.0f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private int tapCount = 0;
    private float firstTapTime = 0f;
    private bool isLeaving = false;
    private bool gameStarted = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
            return;

        StartCoroutine(EnableAfterDelay());
    }

    IEnumerator EnableAfterDelay()
    {
        yield return new WaitForSeconds(enableDelayAfterSpawn);
        gameStarted = true;

        if (showDebugLogs)
            Debug.Log("TripleTapExit: enabled");
    }

    void Update()
    {
        if (!IsOwner || isLeaving || !gameStarted)
            return;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            RegisterTap();
        }
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                RegisterTap();
            }
        }
#endif
    }

    void RegisterTap()
    {
        if (tapCount == 0)
        {
            tapCount = 1;
            firstTapTime = Time.time;

            if (showDebugLogs)
                Debug.Log("TripleTapExit: tap 1");

            return;
        }

        if (Time.time - firstTapTime <= tapWindow)
        {
            tapCount++;

            if (showDebugLogs)
                Debug.Log("TripleTapExit: tap " + tapCount);

            if (tapCount >= 3)
            {
                LeaveGame();
            }
        }
        else
        {
            tapCount = 1;
            firstTapTime = Time.time;

            if (showDebugLogs)
                Debug.Log("TripleTapExit: tap reset, new tap 1");
        }
    }

    void LeaveGame()
    {
        if (isLeaving)
            return;

        isLeaving = true;
        gameStarted = false;

        if (showDebugLogs)
            Debug.Log("TripleTapExit: triple tap detected, shutting down client");

        StartCoroutine(LeaveRoutine());
    }

    IEnumerator LeaveRoutine()
    {
        if (NetworkManager.Singleton != null)
        {
            if (showDebugLogs)
                Debug.Log("TripleTapExit: NetworkManager shutdown");

            NetworkManager.Singleton.Shutdown();
        }

        yield return null;
        yield return null;

        if (showDebugLogs)
            Debug.Log("TripleTapExit: quitting app");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}