// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using UnityEditor;

    [CustomEditor(typeof(VersionInfo))]
    internal class VersionInfoEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            DrawPropertiesExcluding(serializedObject, "m_Script", "storedSdkRevisionHash");
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Resolved SDK Version", RuntimeSettings.Instance.SdkVersion);
            EditorGUILayout.LabelField("Resolved RS Version", RuntimeSettings.Instance.RsVersion);
        }
    }
}
