using UnityEngine;
using System.Diagnostics; // for StackTrace only

public class TimeScaleGuard : MonoBehaviour
{
    float last;

    void Awake()
    {
        last = Time.timeScale;
        UnityEngine.Debug.Log($"[TSGuard] start: {last}");
    }

    void Update()
    {
        if (!Mathf.Approximately(last, Time.timeScale))
        {
            UnityEngine.Debug.LogError($"[TSGuard] timeScale {last} -> {Time.timeScale}\n{new StackTrace(true)}");
            last = Time.timeScale;
        }
    }
}

