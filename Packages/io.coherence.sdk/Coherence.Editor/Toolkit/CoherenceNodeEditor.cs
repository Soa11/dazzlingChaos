// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(CoherenceNode))]
    internal class CoherenceNodeEditor : BaseEditor
    {
        protected override void OnGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            _ = serializedObject.ApplyModifiedProperties();

            var issues = GetIssues();
            var status = new GameObjectStatus(((Component)target).gameObject);
            CoherenceNodeValidator.HandleDrawFixInterpolationLocationHelpBox(issues, serializedObject, status, GUIContents.InvalidInterpolationLocationMessage);
            CoherenceNodeValidator.HandleDrawFixRigidbodyUpdateModeHelpBox(issues, serializedObject, status, GUIContents.InvalidRigidbodyUpdateModeMessage);
        }

        internal CoherenceNodeValidator.Issue GetIssues()
        {
            var issues = CoherenceNodeValidator.Issue.None;

            foreach (var targetObject in targets)
            {
                if (!targetObject || targetObject is not CoherenceNode node || !node.TryGetComponent(out CoherenceSync sync))
                {
                    continue;
                }

                issues |= CoherenceNodeValidator.Validate(sync);
            }

            return issues;
        }

        private static class GUIContents
        {
            public const string InvalidInterpolationLocationMessage = "CoherenceSync's 'Interpolation Location' must not be set to 'None'.";
            public const string InvalidRigidbodyUpdateModeMessage = "CoherenceSync's 'Rigidbody Update Mode' must be set to 'Direct'.";
        }
    }
}
