// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Linq;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Bindings;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;

    internal static class NetworkObjectsInfoDrawer
    {
        private static float padding => 5f;

        internal static void DrawRemoveInvalidBindingsButton(CoherenceSync sync, GameObjectStatus status, int invalid, Action onDeletedBindings)
        {
            if (invalid <= 0)
            {
                return;
            }

            var content = EditorGUIUtility.TrTextContentWithIcon($"This networked object contains {invalid} invalid binding{(invalid != 1 ? "s" : "")}. It may cause instability.", string.Empty, "Warning");

            EditorGUILayout.LabelField(content, ContentUtils.GUIStyles.wrappedLabel);

            if (Application.isPlaying)
            {
                return;
            }

            using var disabledScope = new EditorGUI.DisabledGroupScope(CloneMode.Enabled && !CloneMode.AllowEdits);

            if (!status.IsRootOfAssetHierarchy)
            {
                if (GUILayout.Button("Fix in Prefab Asset", ContentUtils.GUIStyles.bigButton))
                {
                    Selection.activeGameObject = CoherenceSyncEditor.GetPrefab(sync.gameObject);
                    GUIUtility.ExitGUI();
                }

                return;
            }

            if (GUILayout.Button("Remove All Invalid Bindings", ContentUtils.GUIStyles.bigButton))
            {
                var invalidBindings = sync.Bindings.Where(binding => !CoherenceSyncUtils.IsBindingValid(sync, binding, out _)).ToArray();

                if (!EditorUtility.DisplayDialog("Remove All Invalid Bindings?",
                    "Remove the following invalid bindings?\n\n" +
                    string.Join("\n", invalidBindings.Select(b =>
                    {
                        return GetTypeNamePrefix(b.Descriptor.OwnerAssemblyQualifiedName) + b.SignaturePlainText;

                        static string GetTypeNamePrefix(string ownerAssemblyQualifiedName)
                        {
                            if (string.IsNullOrEmpty(ownerAssemblyQualifiedName))
                            {
                                return "";
                            }

                            var end = ownerAssemblyQualifiedName.IndexOf(',');
                            if (end is -1)
                            {
                                return ownerAssemblyQualifiedName + ".";
                            }

                            var typeName = ownerAssemblyQualifiedName.Substring(0, end);
                            var start = typeName.LastIndexOf('.');
                            if (start is -1)
                            {
                                return typeName + ".";
                            }

                            return typeName.Substring(start + 1) + ".";
                        }
                    })),
                    "Confirm", "Cancel"))
                {
                    return;
                }

                var removedCount = CoherenceSyncUtils.RemoveBindings(sync, invalidBindings);
                if (removedCount > 0)
                {
                    Debug.Log($"Removed {removedCount} / {invalidBindings.Length} invalid bindings from '{sync.name}'.", sync);
                }

                onDeletedBindings?.Invoke();
            }
        }

        internal static void DrawRemoveInvalidBindingsFromRegistryButton(int invalid, Action onDeletedBindings)
        {
            if (invalid <= 0)
            {
                return;
            }

            var content = EditorGUIUtility.TrTextContentWithIcon($"There {(invalid is 1 ? "is an invalid binding" : $"are {invalid} invalid bindings")}. It may cause instability.", string.Empty, "Warning");

            EditorGUILayout.LabelField(content, ContentUtils.GUIStyles.richMiniLabel);

            if (GUILayout.Button("Remove All Invalid Bindings", ContentUtils.GUIStyles.bigButton))
            {
                var confirmedAll = false;
                var canceled = false;

                foreach (var config in CoherenceSyncConfigRegistry.Instance)
                {
                    var removedCount = CoherenceSyncUtils.RemoveInvalidBindings(config.Sync, ConfirmRemove);
                    if (removedCount > 0)
                    {
                        Debug.Log($"Removed {removedCount} invalid bindings from '{config.Sync.name}'.", config.Sync);
                    }

                    bool ConfirmRemove(Binding[] invalidBindings)
                    {
                        if (confirmedAll)
                        {
                            return true;
                        }

                        if (canceled)
                        {
                            return false;
                        }

                        const int pickedConfirm = 0;
                        const int pickedCancel = 1;
                        const int pickedConfirmAll = 2;
                        bool confirmed;
                        (confirmed, canceled, confirmedAll) = EditorUtility.DisplayDialogComplex("Remove Invalid Bindings?",
                            $"Remove {invalidBindings.Length} invalid bindings from '{config.Sync.name}'?\n\n" +
                            string.Join("\n", invalidBindings.Select(b =>
                            {
                                return GetTypeNamePrefix(b.Descriptor.OwnerAssemblyQualifiedName) + b.SignaturePlainText;

                                static string GetTypeNamePrefix(string ownerAssemblyQualifiedName)
                                {
                                    if (string.IsNullOrEmpty(ownerAssemblyQualifiedName))
                                    {
                                        return "";
                                    }

                                    var end = ownerAssemblyQualifiedName.IndexOf(',');
                                    if (end is -1)
                                    {
                                        return ownerAssemblyQualifiedName + ".";
                                    }

                                    var typeName = ownerAssemblyQualifiedName.Substring(0, end);
                                    var start = typeName.LastIndexOf('.');
                                    if (start is -1)
                                    {
                                        return typeName + ".";
                                    }

                                    return typeName.Substring(start + 1) + ".";
                                }
                            })),
                            "Confirm", "Cancel", "Confirm All") switch
                        {
                            pickedConfirm => (true, false, false),
                            pickedCancel => (false, true, false),
                            pickedConfirmAll => (true, false, true),
                            var index => throw new IndexOutOfRangeException(index.ToString()),
                        };

                        return confirmed;
                    }
                }

                onDeletedBindings?.Invoke();
            }
        }

        internal static void DrawBindingsWithInputAuthorityPrediction(CoherenceSync sync, int bindingsWithInputAuth, Action onFixedBindings)
        {
            if (bindingsWithInputAuth == 0 || sync.TryGetComponent<CoherenceInput>(out var input))
            {
                return;
            }

            var warnLabel = $"You have ({bindingsWithInputAuth}) bindings with input authority prediction, but you're not using the CoherenceInput component.";

            using var scope = new EditorGUILayout.VerticalScope();
            DrawBackground(scope.rect);

            var content = EditorGUIUtility.TrTextContentWithIcon(warnLabel, string.Empty, "Warning");

            EditorGUILayout.LabelField(content, ContentUtils.GUIStyles.richMiniLabel);

            if (CoherenceHubLayout.DrawButton("Remove Client Prediction"))
            {
                foreach (var binding in sync.Bindings)
                {
                    if (binding.predictionMode == PredictionMode.InputAuthority)
                    {
                        binding.predictionMode = PredictionMode.Never;
                    }
                }

                EditorUtility.SetDirty(sync);

                onFixedBindings?.Invoke();
            }

            EditorGUILayout.Separator();
        }

        internal static void DrawMissingAssets(int missingAssets, Action onClick)
        {
            if (missingAssets == 0)
            {
                return;
            }

            using var scope = new EditorGUILayout.VerticalScope();
            DrawBackground(scope.rect);

            var message = missingAssets > 0 ? $"Found {missingAssets} missing objects." : string.Empty;
            var content = EditorGUIUtility.TrTextContentWithIcon(message, string.Empty, "Warning");

            EditorGUILayout.LabelField(content);
            if (CoherenceHubLayout.DrawButton("View in Registry"))
            {
                Selection.activeObject = CoherenceSyncConfigRegistry.Instance;
                onClick?.Invoke();
            }
            EditorGUILayout.Separator();
        }

        internal static void DrawMissingFromPreloadedAssets()
        {
            var registry = CoherenceSyncConfigRegistry.Instance;
            var assets = PlayerSettings.GetPreloadedAssets();

            if (AssetDatabase.Contains(registry) && !assets.Contains(registry))
            {
                using var scope = new EditorGUILayout.VerticalScope();
                DrawBackground(scope.rect);

                var content = EditorGUIUtility.TrTextContentWithIcon(
                    "CoherenceSyncConfigRegistry should be part of Unity preloaded assets.", string.Empty, "Warning");

                EditorGUILayout.LabelField(content);

                if (CoherenceHubLayout.DrawButton("Add To Preloaded Assets"))
                {
                    for (int i = 0; i < assets.Length; i++)
                    {
                        if (!assets[i])
                        {
                            ArrayUtility.RemoveAt(ref assets, i);
                            i--;
                        }
                    }

                    ArrayUtility.Add(ref assets, registry);
                    PlayerSettings.SetPreloadedAssets(assets);
                }

                EditorGUILayout.Separator();
            }
        }

        private static void DrawAssets(int assetsCount, bool isFiltered)
        {
            var content = EditorGUIUtility.TrTempContent(
                $"{(assetsCount != 0 ? assetsCount.ToString() : "No")} <color=grey>{(isFiltered ? "filtered assets" : "assets")}</color>");

            var width = ContentUtils.GUIStyles.richMiniLabel.CalcSize(content).x;
            var controlRect = EditorGUILayout.GetControlRect(false,
                ContentUtils.GUIStyles.richMiniLabel.CalcHeight(content, width + padding),
                ContentUtils.GUIStyles.richMiniLabel, GUILayout.Width(width + padding));

            EditorGUI.LabelField(controlRect, content, ContentUtils.GUIStyles.richMiniLabel);
        }

        private static void DrawVariables(int variables)
        {
            var content = EditorGUIUtility.TrTempContent(
                $"{(variables != 0 ? variables.ToString() : "No")} <color=grey>variable{(variables != 1 ? "s" : "")}</color>");

            var width = ContentUtils.GUIStyles.richMiniLabel.CalcSize(content).x;

            var controlRect = EditorGUILayout.GetControlRect(false,
                ContentUtils.GUIStyles.richMiniLabel.CalcHeight(content, width + padding),
                ContentUtils.GUIStyles.richMiniLabel, GUILayout.Width(width + padding));

            EditorGUI.LabelField(controlRect, content, ContentUtils.GUIStyles.richMiniLabel);
        }

        private static void DrawBackground(Rect rect)
        {
            string colorHex = EditorGUIUtility.isProSkin ? "#3f3f3f" : "#c8c8c8";

            ColorUtility.TryParseHtmlString(colorHex, out Color color);

            EditorGUI.DrawRect(rect, color);
        }

        private static void DrawMethods(int methods)
        {
            var content = EditorGUIUtility.TrTempContent(
                $"{(methods != 0 ? methods.ToString() : "No")} <color=grey>method{(methods != 1 ? "s" : "")}</color>");

            var width = ContentUtils.GUIStyles.richMiniLabel.CalcSize(content).x;

            var controlRect = EditorGUILayout.GetControlRect(false,
                ContentUtils.GUIStyles.richMiniLabel.CalcHeight(content, width + padding),
                ContentUtils.GUIStyles.richMiniLabel, GUILayout.Width(width + padding));

            EditorGUI.LabelField(controlRect, content, ContentUtils.GUIStyles.richMiniLabel);
        }

        private static void DrawComponentActions(int componentActions)
        {
            GUIContent content = EditorGUIUtility.TrTempContent(
                $"{(componentActions != 0 ? componentActions.ToString() : "No")} <color=grey>component action{(componentActions != 1 ? "s" : "")}</color>");
            var width = ContentUtils.GUIStyles.richMiniLabel.CalcSize(content).x;

            var controlRect = EditorGUILayout.GetControlRect(false,
                ContentUtils.GUIStyles.richMiniLabel.CalcHeight(content, width + padding),
                ContentUtils.GUIStyles.richMiniLabel, GUILayout.Width(width + padding));

            EditorGUI.LabelField(controlRect, content, ContentUtils.GUIStyles.richMiniLabel);
        }
    }
}
