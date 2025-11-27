// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEditor;

    [CanEditMultipleObjects]
    internal class CoherenceQueryEditor : BaseEditor
    {
        private CoherenceQuery query;
        private GameObjectStatus gameObjectStatus;

        protected override void OnEnable()
        {
            base.OnEnable();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        protected override void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            base.OnDisable();
        }

        private void OnHierarchyChanged() => Setup();

        private void Setup()
        {
            query = (CoherenceQuery)target;
            gameObjectStatus = new(query.gameObject);
        }

        protected override void OnGUI()
        {
            if (!ReferenceEquals(target, query))
            {
                Setup();
            }

            CoherenceQueryValidator.DrawIssueHelpBoxes(query, gameObjectStatus);
        }
    }
}
