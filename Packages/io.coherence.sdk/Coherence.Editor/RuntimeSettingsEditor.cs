// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Reflection;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(RuntimeSettings))]
    internal class RuntimeSettingsEditor : BaseEditor, IOnAfterPreloaded
    {
        private string schemaID;
        private SerializedProperty organizationID;
        private SerializedProperty projectID;
        private SerializedProperty projectName;
        private SerializedProperty runtimeKey;
        private SerializedProperty schemas;
        private SerializedProperty extraSchemas;
        private SerializedProperty advancedSettings;
        private SerializedProperty logSettings;
        private SerializedProperty versionInfo;
        private SerializedProperty rsVersionOverride;

        void IOnAfterPreloaded.OnAfterPreloaded()
        {
            var rs = target as RuntimeSettings;
            if (rs == null)
            {
                return;
            }

            rs.VersionInfo = AssetDatabase.LoadAssetAtPath<VersionInfo>(Paths.versionInfoPath);

            Postprocessor.UpdateRuntimeSettings();

            EditorUtility.SetDirty(rs);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            schemaID = TypeUtils.GetFieldValue<string>(serializedObject.targetObject, "schemaID",
                BindingFlags.Instance | BindingFlags.NonPublic);
            organizationID = serializedObject.FindProperty("organizationID");
            projectID = serializedObject.FindProperty("projectID");
            projectName = serializedObject.FindProperty("projectName");
            runtimeKey = serializedObject.FindProperty("runtimeKey");
            schemas = serializedObject.FindProperty("schemas");
            extraSchemas = serializedObject.FindProperty("extraSchemas");
            advancedSettings = serializedObject.FindProperty("advancedSettings");
            logSettings = serializedObject.FindProperty("logSettings");
            versionInfo = serializedObject.FindProperty("versionInfo");
            rsVersionOverride = serializedObject.FindProperty("rsVersionOverride");
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            organizationID.Dispose();
            projectID.Dispose();
            projectName.Dispose();
            runtimeKey.Dispose();
            schemas.Dispose();
            extraSchemas.Dispose();
            advancedSettings.Dispose();
            logSettings.Dispose();
            versionInfo.Dispose();
            rsVersionOverride.Dispose();
        }

        protected override void OnGUI()
        {
            serializedObject.Update();

#if COHERENCE_USE_BAKED_SCHEMA_ID
            DrawSelectableLabel("Schema ID", ValueOrDefault(schemaID));
#else
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSelectableLabel("Schema ID", ValueOrDefault(schemaID));
                if (GUILayout.Button(Icons.GetContent("Coherence.Sync"), EditorStyles.miniButton,
                        GUILayout.ExpandWidth(false)))
                {
                    GUIUtility.keyboardControl = 0;
                    Postprocessor.UpdateRuntimeSettings();
                }

                if (BakeUtil.Outdated)
                {
                    GUILayout.Label(EditorGUIUtility.TrIconContent("Warning", "Outdated schemas, please bake again."));
                }
            }
#endif
            DrawSelectableLabel("Organization ID", ValueOrDefault(organizationID.stringValue));
            DrawSelectableLabel("Project ID", ValueOrDefault(projectID.stringValue));
            DrawSelectableLabel("Project Name", ValueOrDefault(projectName.stringValue));
            DrawSelectableLabel("Runtime Key", ValueOrDefault(runtimeKey.stringValue));

            EditorGUI.BeginDisabledGroup(versionInfo.objectReferenceValue);
            EditorGUILayout.PropertyField(versionInfo);
            EditorGUI.EndDisabledGroup();

            DrawPropertiesExcluding(serializedObject,
                "m_Script",
                "schemaID",
                "uploadedSchema",
                "organizationID",
                "projectID",
                "projectName",
                "runtimeKey",
                "advancedSettings",
                "logSettings",
                "schemas",
                "extraSchemas",
                "organizationName",
                "localDevelopmentMode",
                "versionInfo",
                "defaultAuthorityTransferType",
                "playApiEndpoint",
                "rsVersionOverride",
                "storedSdkVersionBuildMetadata");

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(schemas);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(extraSchemas);

            if (RuntimeSettings.AdvancedSettings.Enabled)
            {
                EditorGUILayout.PropertyField(advancedSettings);
            }

            _ = serializedObject.ApplyModifiedProperties();
        }

        private static string ValueOrDefault(string text) => string.IsNullOrEmpty(text) ? "–" : text;

        private void DrawSelectableLabel(string prefix, string text)
        {
            _ = EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(prefix, EditorStyles.label);
            EditorGUILayout.SelectableLabel(text, EditorStyles.label,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }
    }
}
