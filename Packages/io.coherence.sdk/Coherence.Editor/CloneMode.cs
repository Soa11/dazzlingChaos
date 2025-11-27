namespace Coherence.Editor
{
    using System;
    using UnityEditor;

    /// <summary>
    /// Handles Clone Mode for the current Editor instance. Clone Mode disables asset post-processing and
    /// a few mechanisms like baking. It's recommended to enable Clone Mode on any additional Editor instance opened
    /// for a project, so that there's no chance for asset pipeline automations to hit on those instances (referred to
    /// as Clones).
    /// </summary>
    [InitializeOnLoad]
    public static class CloneMode
    {
        private const string AllowEditsKey = "Coherence.CloneMode.AllowEdits";

        /// <summary>
        /// Triggered when <see cref="Enabled"/> or <see cref="AllowEdits"/> are set.
        /// </summary>
        public static event Action OnChanged;

        private static bool enabled;

        /// <summary>
        /// Enables or disables Clone Mode.
        /// </summary>
        public static bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                OnChanged?.Invoke();
            }
        }

        /// <summary>
        /// Allows modification of assets through the Editor interface when Clone Mode is <see cref="Enabled"/>.
        /// </summary>
        public static bool AllowEdits
        {
            get => UserSettings.GetBool(AllowEditsKey, false);
            set
            {
                UserSettings.SetBool(AllowEditsKey, value);
                OnChanged?.Invoke();
            }
        }

        static CloneMode()
        {
#if HAS_PARRELSYNC
            Enabled |= ParrelSync.ClonesManager.IsClone();
#endif

#if HAS_MPPM
            Enabled |= Array.IndexOf(Environment.GetCommandLineArgs(), "--virtual-project-clone") != -1;
#endif
        }
    }
}
