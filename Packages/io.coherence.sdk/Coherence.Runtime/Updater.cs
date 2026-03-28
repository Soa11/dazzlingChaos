// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Runtime
{
    using System;
#if UNITY
    using Coherence.Utils;
    using UnityEngine;
#endif

    internal interface IUpdatable
    {
        void Update();
    }

    internal interface ILateUpdatable
    {
        void LateUpdate();
    }

    static internal class Updater
    {
#if UNITY
        private static readonly Values<WeakReference<IUpdatable>> UpdateList = new();
        private static readonly Values<WeakReference<ILateUpdatable>> LateUpdateList = new();
        private static readonly object ThreadLock = new();
        private static Action executeOnMainThread;

        static Updater()
        {
            Application.quitting += OnApplicationQuitting;

            void OnApplicationQuitting()
            {
                UpdateList.Clear();
                LateUpdateList.Clear();
            }
        }
#endif

#if !UNITY
        [System.Diagnostics.Conditional("FALSE")]
#endif
        internal static void RegisterForUpdate(IUpdatable item)
        {
#if UNITY
            UpdateList.Add(new(item));
#endif
        }

#if !UNITY
        [System.Diagnostics.Conditional("FALSE")]
#endif
        internal static void ExecuteOnMainThread(Action item)
        {
#if UNITY
            lock (ThreadLock)
            {
                executeOnMainThread += item;
            }
#endif
        }

#if !UNITY
        [System.Diagnostics.Conditional("FALSE")]
#endif
        internal static void DeregisterForUpdate(IUpdatable item)
        {
#if UNITY
            for (var i = UpdateList.Count - 1; i >= 0; i--)
            {
                if (UpdateList[i].TryGetTarget(out var updatable) && item == updatable)
                {
                    UpdateList.RemoveAt(i);
                    break;
                }
            }
#endif
        }

#if !UNITY
        [System.Diagnostics.Conditional("FALSE")]
#endif
        internal static void RegisterForLateUpdate(ILateUpdatable item)
        {
#if UNITY
            LateUpdateList.Add(new(item));
#endif
        }

#if !UNITY
        [System.Diagnostics.Conditional("FALSE")]
#endif
        internal static void DeregisterForLateUpdate(ILateUpdatable item)
        {
#if UNITY
            for (var i = LateUpdateList.Count - 1; i >= 0; i--)
            {
                if (LateUpdateList[i].TryGetTarget(out var lateUpdatable) && item == lateUpdatable)
                {
                    LateUpdateList.RemoveAt(i);
                    break;
                }
            }
#endif
        }

#if UNITY
        internal static void UpdateAll()
        {
            foreach (var reference in UpdateList)
            {
                if (reference.TryGetTarget(out var lateUpdatable))
                {
                    lateUpdatable.Update();
                }
                else
                {
                    UpdateList.Remove(reference);
                }
            }

            if (executeOnMainThread is not null)
            {
                lock (ThreadLock)
                {
                    var action = executeOnMainThread;
                    executeOnMainThread = null;
                    action?.Invoke();
                }
            }
        }

        internal static void LateUpdateAll()
        {
            foreach (var reference in LateUpdateList)
            {
                if (reference.TryGetTarget(out var lateUpdatable))
                {
                    lateUpdatable.LateUpdate();
                }
                else
                {
                    LateUpdateList.Remove(reference);
                }
            }
        }
#endif
    }
}
