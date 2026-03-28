// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Debugging
{
    using System.Diagnostics;

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.Build;
#endif

#if UNITY_5_3_OR_NEWER
    using AssertImpl = UnityDbgAssert;
#else
    using AssertImpl = SystemDbgAssert;
#endif

    public static class DbgAssert
    {
#if UNITY_5_3_OR_NEWER
        public const string ASSERTIONS_ENABLED = "COHERENCE_ASSERTIONS";
#else
        public const string ASSERTIONS_ENABLED = "DEBUG";
#endif

        public const bool EnabledAtCompileTime =
#if COHERENCE_ASSERTIONS
            true;
#else
            false;
#endif

#if !UNITY_EDITOR
        private static bool enabled = true;
        public static bool Enabled { get => enabled; set => enabled = value; }
#else
        private static bool enabled = true;
        public static bool Enabled {
            get => enabled;
            set
            {
                if (enabled == value)
                {
                    return;
                }

                enabled = value;
                WasEnabled = value;
            }
        }

        private const string EnabledPrefsKey = "Coherence.Debugging.DbgAssert.Enabled";
        private static bool WasEnabled
        {
            get => EditorPrefs.GetBool(EnabledPrefsKey, true);
            set => EditorPrefs.SetBool(EnabledPrefsKey, value);
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            enabled = WasEnabled;
        }

        internal static bool EnableAtCompileTime(NamedBuildTarget? target = null)
        {
            var endTarget = target ?? NamedBuildTarget.Standalone;

            var defines = PlayerSettings.GetScriptingDefineSymbols(endTarget);
            if (defines.Contains(ASSERTIONS_ENABLED))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(defines))
            {
                defines += ";" + ASSERTIONS_ENABLED;
            }
            else
            {
                defines = ASSERTIONS_ENABLED;
            }

            PlayerSettings.SetScriptingDefineSymbols(endTarget, defines);

            UnityEngine.Debug.Log($"[coherence] Added {ASSERTIONS_ENABLED} define symbol for {endTarget.TargetName}");

            return true;
        }

        internal static bool DisableAtCompileTime(NamedBuildTarget? target = null)
        {
            var endTarget = target ?? NamedBuildTarget.Standalone;

            var defines = PlayerSettings.GetScriptingDefineSymbols(endTarget);
            if (!defines.Contains(ASSERTIONS_ENABLED))
            {
                return false;
            }

            var symbols = defines.Split(';');
            var filtered = System.Array.FindAll(symbols, s => s != ASSERTIONS_ENABLED);
            var newDefines = string.Join(";", filtered);

            PlayerSettings.SetScriptingDefineSymbols(endTarget, newDefines);

            UnityEngine.Debug.Log($"[coherence] Removed {ASSERTIONS_ENABLED} define symbol for {endTarget.TargetName}");

            return true;
        }
#endif // UNITY_EDITOR

        [Conditional(ASSERTIONS_ENABLED)]
        public static void That(bool condition, string message)
        {
            if (!enabled)
            {
                return;
            }

            AssertImpl.That(condition, message);
        }

        [Conditional(ASSERTIONS_ENABLED)]
        public static void That(bool condition, ref bool triggeredOnce, string message)
        {
            if (!enabled || triggeredOnce)
            {
                return;
            }

            AssertImpl.That(condition, message);
            triggeredOnce = !condition;
        }

        [Conditional(ASSERTIONS_ENABLED)]
        public static void ThatFmt<T1>(bool condition, ref bool triggeredOnce, string messageToFormat, in T1 arg1)
        {
            if (!enabled || triggeredOnce)
            {
                return;
            }

            AssertImpl.ThatFmt(condition, messageToFormat, arg1);
            triggeredOnce = !condition;
        }

        [Conditional(ASSERTIONS_ENABLED)]
        public static void ThatFmt<T1>(bool condition, string messageToFormat, in T1 arg1)
        {
            if (!enabled)
            {
                return;
            }

            AssertImpl.ThatFmt(condition, messageToFormat, arg1);
        }

        [Conditional(ASSERTIONS_ENABLED)]
        public static void ThatFmt<T1, T2>(bool condition, string messageToFormat, in T1 arg1, in T2 arg2)
        {
            if (!enabled)
            {
                return;
            }

            AssertImpl.ThatFmt(condition, messageToFormat, arg1, arg2);
        }

        [Conditional(ASSERTIONS_ENABLED)]
        public static void ThatFmt<T1, T2, T3>(bool condition, string messageToFormat, in T1 arg1, in T2 arg2, in T3 arg3)
        {
            if (!enabled)
            {
                return;
            }

            AssertImpl.ThatFmt(condition, messageToFormat, arg1, arg2, arg3);
        }

        [Conditional(ASSERTIONS_ENABLED)]
        public static void ThatFmt<T1, T2, T3, T4>(bool condition, string messageToFormat, in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4)
        {
            if (!enabled)
            {
                return;
            }

            AssertImpl.ThatFmt(condition, messageToFormat, arg1, arg2, arg3, arg4);
        }
    }
}

