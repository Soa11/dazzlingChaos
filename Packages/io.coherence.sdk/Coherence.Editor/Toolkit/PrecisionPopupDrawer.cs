// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using UnityEngine;
    using UnityEditor;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Archetypes;
    using Coherence.Toolkit.Bindings;

    internal class PrecisionPopupDrawer
    {
        private static int PopupWidth = 300;
        private static int PopupHeight = 60;

        private static double? Precision;

        protected static BindingLODStepData cachedData;
        protected static BindingsWindow BindingsWindow;

        public static double Draw(Rect rect, ArchetypeComponent boundComponent, Binding binding, BindingArchetypeData archetypeData, BindingLODStepData lodStep,
            int step, BindingsWindow bindingsWindow, BindingArchetypeData basePrefabArchetypeData, string unit)
        {
            BindingsWindow = bindingsWindow;

            if (GUI.Button(rect, GUIContent.none, EditorStyles.textArea))
            {
                if (Event.current.button == 0)
                {
                    var r = new Rect(rect.x, rect.yMax, 0, 0);
                    cachedData = lodStep;
                    Precision = cachedData.Precision;
                    ShowPopup(r, archetypeData, lodStep);
                }
                else if (Event.current.button == 1
                         && basePrefabArchetypeData != null
                         && basePrefabArchetypeData.Fields.Count > step
                         && (Math.Abs(lodStep.Precision - basePrefabArchetypeData.GetLODstep(step).Precision) > 0.00001d
                             || lodStep.Bits != basePrefabArchetypeData.GetLODstep(step).Bits))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new("Revert Prefab Override"), false, () =>
                    {
                        if (basePrefabArchetypeData.GetLODstep(step) is not { } basePrefabArchetypeLodStep)
                        {
                            Debug.LogWarning($"Could not find LOD step {step} on base prefab archetype data of '{bindingsWindow.Component}'.");
                            return;
                        }

                        Undo.RecordObject(bindingsWindow.Component, "Revert LOD Precision");
                        lodStep.SetPrecision(basePrefabArchetypeLodStep.Precision);
                        lodStep.SetBits(basePrefabArchetypeLodStep.Bits);
                        bindingsWindow.Repaint();
                    });

                    menu.AddItem(new("Apply To Base Prefab"), false, () =>
                    {
                        var sync = boundComponent.Component as CoherenceSync;
                        if (!sync)
                        {
                            sync = boundComponent.Component.GetComponentInParent<CoherenceSync>(true);
                        }

                        if (!sync)
                        {
                            Debug.LogWarning($"Could not find CoherenceSync of '{bindingsWindow.Component}'.");
                            return;
                        }

                        var basePrefabSync = PrefabUtils.FindBasePrefab(sync);
                        if (!basePrefabSync)
                        {
                            Debug.LogWarning($"Could not find base prefab of '{bindingsWindow.Component}'.");
                            return;
                        }

                        var basePrefabBindings = basePrefabSync.Bindings;
                        var basePrefabBindingIndex = basePrefabBindings.FindIndex(x => x.guid == binding.guid);
                        if (basePrefabBindingIndex is -1)
                        {
                            Debug.LogWarning($"Could not find binding '{binding.Name}' on base prefab '{basePrefabSync}'.");
                            return;
                        }

                        Undo.RecordObject(basePrefabSync, "Apply LOD Precision To Base Prefab");
                        using var serializedObject = new SerializedObject(basePrefabSync);
                        using var bindingsProperty = serializedObject.FindProperty(CoherenceSync.Property.bindings);
                        using var bindingProperty = bindingsProperty.GetArrayElementAtIndex(basePrefabBindingIndex);
                        using var archeTypeDataProperty = bindingProperty.FindPropertyRelative(Binding.Property.archetypeData);
                        using var lodStepsProperty = archeTypeDataProperty.FindPropertyRelative(BindingArchetypeData.Property.fields);
                        using var lodStepProperty = lodStepsProperty.GetArrayElementAtIndex(step);
                        using var precisionProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.precision);
                        precisionProperty.doubleValue = lodStep.Precision;
                        using var bitsProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.bits);
                        bitsProperty.intValue = lodStep.Bits;
                        serializedObject.ApplyModifiedProperties();

                        if (basePrefabArchetypeData.GetLODstep(step) is { } basePrefabArchetypeLodStep)
                        {
                            basePrefabArchetypeLodStep.SetPrecision(lodStep.Precision);
                            basePrefabArchetypeLodStep.SetBits(lodStep.Bits);
                        }
                    });
                    menu.ShowAsContext();
                }
            }

            var miniText = new GUIStyle(EditorStyles.miniLabel);
            miniText.alignment = TextAnchor.MiddleLeft;
            miniText.richText = true;
            miniText.fontSize = 9;
            Rect multiplierRect = new Rect(rect.x, rect.y, rect.width, rect.height);

            if (lodStep.Precision > 1f)
            {
                GUI.Label(multiplierRect, $"<color=#888888>+/-</color>{lodStep.Precision:G3}{unit}", miniText);
            }
            else
            {
                GUI.Label(multiplierRect, $"<color=#888888>+/-</color>{lodStep.Precision:G2}{unit}", miniText);
            }

            if (lodStep != cachedData || !Precision.HasValue)
            {
                return -1;
            }

            var precision = Precision.Value;
            Precision = null;
            return precision;
        }

        private static void ShowPopup(Rect rect, BindingArchetypeData archetypeData, BindingLODStepData lodStep)
        {
            var popup = new GenericPopup(() => OnPopupGUI(archetypeData, lodStep), GetPopupSize, OnPopupOpen, OnPopupClose);
            PopupWindow.Show(rect, popup);
            GUIUtility.ExitGUI();
        }

        private static void OnPopupOpen()
        {
        }

        private static void OnPopupGUI(BindingArchetypeData archetypeData, BindingLODStepData data)
        {
            _ = EditorGUILayout.BeginVertical();

            GUILayout.Space(2);

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.richText = true;
            GUILayout.Label($"<color=#888888>Precision:</color> +/-{cachedData.Precision}", style, GUILayout.ExpandWidth(true));
            GUILayout.Space(2);

            GUILayout.Label(GUIContent.none, ContentUtils.GUIStyles.separator);

            double maxPrecision = ArchetypeMath.GetRoundedPrecisionByBitsAndRange(32, archetypeData.TotalRange);
            GUILayout.BeginHorizontal();

            double value = 1f / Mathf.Pow(10, 1);
            for (int i = 1; value >= maxPrecision; i++)
            {
                string title = value.ToString();

                GUIStyle buttonStyle = new GUIStyle(EditorStyles.toolbarButton);
                buttonStyle.fontSize = 9;

                EditorGUI.BeginChangeCheck();
                var sel = GUILayout.Button(title, buttonStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    SetPrecision(value);
                    GenericPopup.Repaint();
                    GUIUtility.ExitGUI();
                }
                value = 1f / Mathf.Pow(10, i + 1);
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static void SetPrecision(double value)
        {
            Precision = value;
            BakeUtil.CoherenceSyncSchemasDirty = true;
            BindingsWindow.Repaint();
        }

        private static void OnPopupClose() { }
        private static Vector2 GetPopupSize() => new(PopupWidth, PopupHeight);
    }
}
