// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Linq;
    using Coherence.Toolkit;
    using Coherence.Utils;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Responsible for validating that a <see cref="CoherenceSync"/> has been configured properly for use with <see cref="CoherenceNode"/>,
    /// and providing methods to automatically fix the issues.
    /// </summary>
    internal static class CoherenceNodeValidator
    {
        public static Issue Validate(CoherenceSync sync)
        {
            var issues = Issue.None;

            if (!IsRigidbodyUpdateModeValid(sync))
            {
                issues |= Issue.InvalidRigidbodyUpdateMode;
            }

            if (!IsInterpolationLocationValid(sync))
            {
                issues |= Issue.InvalidInterpolationLocation;
            }

            return issues;

            static bool IsRigidbodyUpdateModeValid(CoherenceSync coherenceSync) => coherenceSync.RigidbodyUpdateMode is CoherenceSync.RigidbodyMode.Direct;
            static bool IsInterpolationLocationValid(CoherenceSync coherenceSync) => coherenceSync.InterpolationLocationConfig is not default(CoherenceSync.InterpolationLoop);
        }

        public static void HandleDrawFixInterpolationLocationHelpBox(Issue issues, SerializedObject serializedObject, GameObjectStatus status, string message)
        {
            if (issues.HasFlag(Issue.InvalidInterpolationLocation))
            {
                CoherenceSyncEditor.DrawFixHelpBox(serializedObject, status, message, GUIContents.FixInvalidInterpolationLocationButtonLabel, FixInvalidInterpolationLocation);
            }
        }

        public static void HandleDrawFixRigidbodyUpdateModeHelpBox(Issue issues, SerializedObject serializedObject, GameObjectStatus status, string message)
        {
            if (issues.HasFlag(Issue.InvalidRigidbodyUpdateMode))
            {
                CoherenceSyncEditor.DrawFixHelpBox(serializedObject, status, message, GUIContents.FixInvalidRigidbodyUpdateModeButtonLabel, FixRigidBodyUpdateMode);
            }
        }

        public static void DrawFixInterpolationLocationHelpBox(SerializedObject serializedObject, GameObjectStatus status, string message)
            => CoherenceSyncEditor.DrawFixHelpBox(serializedObject, status, message, GUIContents.FixInvalidInterpolationLocationButtonLabel, FixInvalidInterpolationLocation);

        public static void DrawFixRigidbodyUpdateModeHelpBox(SerializedObject serializedObject, GameObjectStatus status, string message)
            => CoherenceSyncEditor.DrawFixHelpBox(serializedObject, status, message, GUIContents.FixInvalidRigidbodyUpdateModeButtonLabel, FixRigidBodyUpdateMode);

        internal static void FixInvalidInterpolationLocation(SerializedObject syncSerializedObject)
        {
            // If all targets have invalid values, fix all of them.
            if (syncSerializedObject.targetObjects.All(x => x is CoherenceSync { InterpolationLocationConfig: default(CoherenceSync.InterpolationLoop) }))
            {
                using var serializedProperty = syncSerializedObject.FindProperty(CoherenceSync.Property.interpolationLocation);
                serializedProperty.intValue = (int)CoherenceSync.InterpolationLoop.Update;
                return;
            }

            // If some targets have valid values, leave those unchanged.
            foreach (var target in syncSerializedObject.targetObjects)
            {
                if (target is not CoherenceSync { InterpolationLocationConfig: default(CoherenceSync.InterpolationLoop) })
                {
                    continue;
                }

                syncSerializedObject.ApplyModifiedProperties();
                using var targetSerializedObject = new SerializedObject(target);
                using var serializedProperty = targetSerializedObject.FindProperty(CoherenceSync.Property.interpolationLocation);
                serializedProperty.intValue = (int)CoherenceSync.InterpolationLoop.Update;
                targetSerializedObject.ApplyModifiedProperties();
                syncSerializedObject.Update();
            }
        }

        internal static void FixRigidBodyUpdateMode(SerializedObject syncSerializedObject)
        {
            using var rigidbodyUpdateModeProperty = syncSerializedObject.FindProperty(CoherenceSync.Property.RigidbodyUpdateMode);
            rigidbodyUpdateModeProperty.intValue = (int)CoherenceSync.RigidbodyMode.Direct;
        }

        [Flags]
        internal enum Issue
        {
            None = FlagsValues._0,
            InvalidRigidbodyUpdateMode = FlagsValues._1,
            InvalidInterpolationLocation = FlagsValues._2
        }

        private static class GUIContents
        {
            public static readonly GUIContent FixInvalidInterpolationLocationButtonLabel = new("Fix Interpolation Location");
            public static readonly GUIContent FixInvalidRigidbodyUpdateModeButtonLabel = new("Fix Rigidbody Update Mode");
        }
    }
}
