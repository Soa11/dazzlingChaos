// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Archetypes;
    using Coherence.Toolkit.Bindings;
    using Interpolation;
    using Log;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;
    using Logger = Log.Logger;
    using Object = UnityEngine.Object;

    internal class BindingsTreeViewBindingItem : BindingsTreeViewItem
    {
        private static readonly Color PropertyOverrideColor = new Color32(15, 129, 190, 255);

        public Binding Binding { get; private set; }
        public ArchetypeComponent BoundComponent => boundComponent;
        public BindingsTreeViewComponentItem ComponentItem { private set; get; }

        public bool SelectedForSync { private set; get; }
        public bool IsMethod => Binding.IsMethod;
        public bool SelectedInTreeView { protected get; set; }

        private BindingsWindow bindingsWindow;
        private ArchetypeComponent boundComponent;
        private BindingArchetypeData archetypeData;
        private readonly CoherenceSync sync;
        private string cantEditString;
        private readonly Binding bindingOnEditorCache;
        private WarningSource warningSource;
        private static readonly LazyLogger logger = Log.GetLazyLogger<BindingsTreeViewBindingItem>();
        private static Logger Logger => logger.Logger;

        internal bool CanOverride { private set; get; }
        internal SchemaType SchemaType { private set; get; }
        [MaybeNull] private BindingArchetypeData basePrefabBindingArchetypeData;
        [MaybeNull] private ToolkitArchetype basePrefabBindingToolkitArchetype;

        internal static Action<BindingsTreeViewInput, BindingsTreeViewBindingItem> OnInput;

        private readonly List<string> warningList = new List<string>();
        private readonly StringBuilder warningsTooltipBuilder = new StringBuilder(512);

        internal BindingsTreeViewBindingItem(CoherenceSync sync, ArchetypeComponent boundComponent, Binding binding)
        {
            this.sync = sync;
            this.boundComponent = boundComponent;

            Binding = binding;
            var basePrefab = PrefabUtils.FindBasePrefab(boundComponent.Component);
            if (basePrefab)
            {
                var basePrefabSync = basePrefab.GetComponentInParent<CoherenceSync>(includeInactive: true);
                if (basePrefabSync)
                {
                    // Without this ToolkitArchetype.BoundComponents might not be populated correctly
                    basePrefabSync.ValidateArchetype();
                    basePrefabBindingToolkitArchetype = basePrefabSync.Archetype;
                    var basePrefabBoundComponent = basePrefabSync.Archetype.BoundComponents.FirstOrDefault(x => ReferenceEquals(x.Component, basePrefab));
                    var basePrefabBinding = basePrefabBoundComponent?.Bindings.FirstOrDefault(x => x.guid == binding.guid);
                    basePrefabBindingArchetypeData = basePrefabBinding?.BindingArchetypeData;
                }
            }

            bindingOnEditorCache = binding;
            displayName = binding.Name;

            SelectedForSync = IsActiveOnSync();
            sync.Archetype.SetBindingActive(binding, boundComponent, SelectedForSync);
            UpdateActiveTarget();
        }

        internal void SetTreeViewData(int id, BindingsTreeViewComponentItem componentItem, BindingsWindow bindingsWindow)
        {
            // TreeviewSpecific
            this.id = id;
            this.bindingsWindow = bindingsWindow;

            var group = IsMethod ? componentItem.Methods : componentItem.Fields;
            group.AddMember(this);
            componentItem.AddChild(this);
            depth = 1;

            ComponentItem = componentItem;

            GetCantEditText();
        }

        protected void SetArchetypeData(ArchetypeComponent boundComponent, BindingArchetypeData archetypeData, bool isMethod)
        {
            this.boundComponent = boundComponent;
            this.archetypeData = archetypeData;

            CanOverride = archetypeData.CanOverride(isMethod);
            SchemaType = archetypeData.SchemaType;
            Setup(rowHeight: 22, lodSteps: boundComponent.MaxLods);

            GetCantEditText();
        }

        private void GetCantEditText()
        {
            if (!CanOverride)
            {
                if (IsMethod)
                {
                    cantEditString = "";
                    return;
                }

                var bits = BindingLODStepData.GetDefaultBits(SchemaType);
                var content = ContentUtils.GetSchemaTypeIcon(SchemaType);
                cantEditString = $"{content.tooltip} is always {bits} {(bits == 1 ? "Bit" : "Bits")}";
            }
        }

        internal void SetBindingActive(bool active)
        {
            if (!GetCanBeSelectedForSync())
            {
                active = true;
            }
            if (active != SelectedForSync || IsActiveOnSync() != active)
            {
                SelectedForSync = active;
                sync.Archetype.SetBindingActive(Binding, boundComponent, active);
            }
        }

        internal GUIContent GetNameAndIconGUIContent() => ContentUtils.GetContent(Binding.UnityComponent, Binding.Descriptor);
        protected bool GetCanBeSelectedForSync() => !Binding.Descriptor.Required;
        protected bool IsActiveOnSync() => sync.Bindings.Contains(bindingOnEditorCache);

        protected void UpdateActiveTarget()
        {
            if (IsActiveOnSync())
            {
                Binding = CoherenceSyncBindingHelper.GetSerializedBinding(sync, bindingOnEditorCache);
            }
            else
            {
                Binding = bindingOnEditorCache;
            }

            SetArchetypeData(boundComponent, Binding.BindingArchetypeData, Binding.IsMethod);
        }

        internal bool GetCanInterpolate() => !Binding.IsMethod;

        internal void SetInterpolation(InterpolationSettings interpolationSettings)
        {
            if (GetCanInterpolate())
            {
                Binding.interpolationSettings = interpolationSettings;
            }
        }

        protected void DrawMethod(Rect rect)
        {
            EditorGUI.BeginChangeCheck();
            MessageTarget routing = (MessageTarget)EditorGUI.EnumPopup(rect, GUIContent.none, ((CommandBinding)Binding).routing);
            if (EditorGUI.EndChangeCheck())
            {
                var edit = new BindingsTreeViewInput(-1, BindingsTreeViewInput.Type.MessageTarget, routing, "Changed Routing");
                ApplyInput(edit);
            }
        }

        internal override void DrawRowBackground(Rect rowRect)
        {
            Color color = SelectedInTreeView ? BindingsWindowSettings.RowSelectedColor : BindingsWindowSettings.RowColor;
            EditorGUI.DrawRect(rowRect, color);
            base.DrawRowBackground(rowRect);
        }

        protected override void DrawLeftBar(Rect rect)
        {
            int iconSize = 14;
            int menuSize = 16;
            int warningSize = 16;
            int inset = 25;

            // Get rects
            Rect iconRect = new Rect(rect.x + 8, rect.y + ((rect.height - iconSize) * .5f), iconSize, iconSize);
            Rect nameRect = new Rect(rect.x + inset, rect.y, rect.width - inset - menuSize, rect.height);
            Rect menuRect = new Rect(rect.xMax - menuSize, rect.y, menuSize, rect.height);

            // Draw Icon
            if (bindingsWindow.Toolbar.Filters.AnyTypeFiltersActive() || bindingsWindow.Toolbar.Filters.PopupOpen)
            {
                DrawIcon(iconRect);
            }
            else
            {
                nameRect.x -= menuSize;
                nameRect.width += menuSize;
            }

            // Draw Warnings
            if (HasWarnings())
            {
                nameRect.width -= warningSize;
                Rect warningRect = new Rect(menuRect.x - warningSize, menuRect.y, warningSize, menuRect.height);

                warningsTooltipBuilder.Length = 0;
                foreach (string warning in warningList)
                {
                    warningsTooltipBuilder.AppendLine(warning);
                }

                warningsTooltipBuilder.AppendLine();
                warningsTooltipBuilder.Append("Click on this icon to automatically fix issues.");

                string helpText = warningsTooltipBuilder.ToString();

                GUIContent icon = EditorGUIUtility.IconContent("Warning");
                GUIContent helpbox = new GUIContent(icon.image, helpText);

                if (GUI.Button(warningRect, helpbox, EditorStyles.label))
                {
                    FixAllWarnings();
                }
            }

            // Draw label/ bindings
            GUIContent content = GetNameAndIconGUIContent();

            if (BindingsWindow.EditingAllFields || ComponentItem.EditingLocalBindings)
            {
                EditorGUI.BeginDisabledGroup(!GetCanBeSelectedForSync());
                Rect toggleRect = nameRect.SplitX(true, 20);
                nameRect = nameRect.SplitX(false, 20);

                bool synced = EditorGUI.Toggle(toggleRect, GUIContent.none, SelectedForSync);
                if (SelectedForSync != synced)
                {
                    BindingsTreeViewInput input = new BindingsTreeViewInput(-1, BindingsTreeViewInput.Type.Selected, synced ? 1 : 0, "Sync selection changed");
                    ApplyInput(input);
                }
                EditorGUI.EndDisabledGroup();
            }

            GUI.Label(nameRect, content, ContentUtils.GUIStyles.richLabel);

            CreateBindingsCommandMenu(menuRect);

            if ((basePrefabBindingArchetypeData is not null
               && !basePrefabBindingArchetypeData.Equals(archetypeData))
               || (basePrefabBindingToolkitArchetype is not null
               && !basePrefabBindingToolkitArchetype.Equals(sync.Archetype)))
            {
                DrawPropertyOverrideIndicator(rect);
            }
        }

        protected override void DrawBindingConfigBar(Rect rect)
        {
            RectOffset padding = new RectOffset(2, 2, 2, 2);
            rect = padding.Remove(rect);

            EditorGUI.BeginDisabledGroup(!SelectedForSync);
            if (IsMethod)
            {
                DrawMethod(rect);
            }
            else
            {
                DrawInterpolation(rect);
            }
            EditorGUI.EndDisabledGroup();
        }

        protected override void DrawCompressionTypeBar(Rect rect)
        {
            if (IsMethod || !CanOverride)
            {
                return;
            }

            if (IsFloatType())
            {
                EditorGUI.BeginChangeCheck();
                var compression = (FloatCompression)EditorGUI.EnumPopup(rect, archetypeData.FloatCompression);
                if (EditorGUI.EndChangeCheck())
                {
                    var edit = new BindingsTreeViewInput(0, BindingsTreeViewInput.Type.Compression, compression, "Changed Compression");
                    ApplyInput(edit);
                }

                if (basePrefabBindingArchetypeData is not null
                    && basePrefabBindingArchetypeData.FloatCompression != compression)
                {
                    DrawPropertyOverrideIndicator(rect);
                }
            }
        }

        protected override void DrawValueRangeBar(Rect rect)
        {
            if (IsMethod)
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(!SelectedForSync);

            RectOffset padding = new RectOffset(2, 2, 2, 2);
            SplitInTwoLayoutRects layout = new SplitInTwoLayoutRects(rect, padding);

            if (CanOverride)
            {
                bool usesRange = SchemaType == SchemaType.Int ||
                    SchemaType == SchemaType.UInt ||
                    (IsFloatType() && archetypeData.FloatCompression == FloatCompression.FixedPoint);
                if (usesRange)
                {
                    DrawIntRangeInput(layout.FirstRect, layout.SecondRect, 0);

                    if (basePrefabBindingArchetypeData is not null &&
                        (basePrefabBindingArchetypeData.MinRange != archetypeData.MinRange
                         || basePrefabBindingArchetypeData.MaxRange != archetypeData.MaxRange))
                    {
                        DrawPropertyOverrideIndicator(rect);
                    }
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        protected override void DrawSampleRateBar(Rect rect)
        {
            if (IsMethod)
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(!SelectedForSync);

            RectOffset padding = new RectOffset(2, 2, 2, 2);
            SplitInTwoLayoutRects layout = new SplitInTwoLayoutRects(rect, padding);

            EditorGUI.BeginChangeCheck();

            var newSampleRate = EditorGUI.DelayedFloatField(layout.FirstRect, archetypeData.SampleRate);

            if (EditorGUI.EndChangeCheck())
            {
                var edit = new BindingsTreeViewInput(0, BindingsTreeViewInput.Type.SampleRate,
                    newSampleRate, "Changed SampleRate");
                ApplyInput(edit);
            }

            GUI.Label(layout.SecondRect, new GUIContent("hz"));

            if (basePrefabBindingArchetypeData is not null
                && !Mathf.Approximately(basePrefabBindingArchetypeData.SampleRate, newSampleRate))
            {
                DrawPropertyOverrideIndicator(rect);
            }

            EditorGUI.EndDisabledGroup();
        }

        protected override void DrawStatisticsBar(Rect rect)
        {
            if (IsMethod)
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(!SelectedForSync);
            var padding = new RectOffset(2, 2, 2, 2);
            rect = padding.Remove(rect);

            var maxLodCount = boundComponent.MaxLods;
            if (maxLodCount > 0)
            {
                var arrayPool = ArrayPool<int>.Shared;
                var values = arrayPool.Rent(maxLodCount);
                var basePrefabValues = arrayPool.Rent(maxLodCount);

                for (var i = 0; i < maxLodCount; i++)
                {
                    var field = archetypeData.GetLODstep(i);
                    values[i] = boundComponent.LodStepsActive > i ? field.Bits : 0;
                    var basePrefabField = basePrefabBindingArchetypeData?.GetLODstep(i) ?? field;
                    basePrefabValues[i] = boundComponent.LodStepsActive > i ? basePrefabField.Bits : 0;
                }

                if (!IsIntType() && SelectedForSync && CanOverride)
                {
                    int maxValue = 32;
                    if (archetypeData.FloatCompression == FloatCompression.FixedPoint)
                    {
                        double maxPrecision = ArchetypeMath.GetRoundedPrecisionByBitsAndRange(32, (uint)(archetypeData.MaxRange - archetypeData.MinRange));
                        ArchetypeMath.TryGetBitsForFixedFloatValue(archetypeData.MinRange, archetypeData.MaxRange, maxPrecision, out maxValue);
                    }

                    EditorGUI.BeginChangeCheck();
                    var multiplier = ArchetypeMath.GetBitsMultiplier(archetypeData.SchemaType);
                    int editedStep = CoherenceArchetypeDrawer.DrawEditableDataWeightMiniBar(rect, ref values, boundComponent.MaxLods, maxValue, multiplier);

                    if (basePrefabBindingArchetypeData is not null
                        && (ArchetypeMath.GetBitsMultiplier(basePrefabBindingArchetypeData.SchemaType) != multiplier
                            || !values.Take(maxLodCount).SequenceEqual(basePrefabValues.Take(maxLodCount))))
                    {
                        var overrideIndicatorRect = rect;
                        overrideIndicatorRect.x -= 2f;
                        overrideIndicatorRect.height += 1f;
                        DrawPropertyOverrideIndicator(overrideIndicatorRect);
                    }

                    if (EditorGUI.EndChangeCheck() && editedStep >= 0)
                    {
                        BindingsTreeViewInput input = new BindingsTreeViewInput(editedStep, BindingsTreeViewInput.Type.Bits, values[editedStep], "Changed Bits");
                        ApplyInput(input);
                    }
                }
                else
                {
                    CoherenceArchetypeDrawer.DrawDataWeightMiniBar(rect, values, boundComponent.MaxLods, CanOverride);

                    if (!CanOverride)
                    {
                        GUIContent toolTip = new GUIContent("", cantEditString);
                        GUI.Label(rect, toolTip);
                    }
                }

                arrayPool.Return(values, true);
                arrayPool.Return(basePrefabValues, true);
            }
            EditorGUI.EndDisabledGroup();
        }

        protected override void DrawLOD(Rect rect, int step)
        {
            if (IsMethod)
            {
                return;
            }

            var enabledOnComponent = boundComponent.LodStepsActive > step;
            if (!enabledOnComponent)
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(!SelectedForSync);

            BindingLODStepData field = archetypeData.GetLODstep(step);
            if (field != null)
            {
                RectOffset padding = new RectOffset(2, 2, 2, 2);
                Rect rectForInput = new Rect(rect);

                if (BindingsWindowSettings.ShowBitPercentages)
                {
                    // Rect changes
                    float width = BindingsWindowSettings.LODBitPercentageWidth;
                    rectForInput.width -= width;

                    Rect textRect = new Rect(rect.xMax - width, rect.y, width, rect.height);

                    DrawBitPercentage(textRect, field.TotalBits, boundComponent.GetTotalBitsOfLOD(step), "", " of the bits of this component");
                }

                LODLayoutRects layout = new LODLayoutRects(rectForInput, BindingsWindowSettings.CanEditLODRanges, padding);

                if (CanOverride)
                {
                    if (IsIntType() && IsRangedType())
                    {
                        DrawIntBits(layout.Bits, field);
                        DrawIntRangeInput(layout.MinRange, layout.MaxRange, step);
                    }
                    else if (IsFloatType())
                    {
                        bool bitsInputDisabled = archetypeData.FloatCompression == FloatCompression.None || archetypeData.FloatCompression == FloatCompression.FixedPoint;
                        if (bitsInputDisabled)
                        {
                            DrawIntBits(layout.Bits, field);
                        }
                        else
                        {
                            DrawBasicBitsInput(layout.Bits, field, step);
                        }

                        if (archetypeData.FloatCompression != FloatCompression.None)
                        {
                            bool precisionInputDisabled = archetypeData.FloatCompression != FloatCompression.FixedPoint;
                            string unit = string.Empty;
                            if (archetypeData.FloatCompression == FloatCompression.Truncated)
                            {
                                unit = "%";
                            }

                            EditorGUI.BeginDisabledGroup(precisionInputDisabled);
                            {
                                DrawPrecisionResult(layout.Precision, field, step, unit);
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                    }
                    else if (SchemaType == SchemaType.Color)
                    {
                        DrawBasicBitsInput(layout.Bits, field, step);

                        EditorGUI.BeginDisabledGroup(true);
                        {
                            DrawPrecisionResult(layout.Precision, field, step);
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    else if (SchemaType == SchemaType.Quaternion)
                    {
                        DrawBasicBitsInput(layout.Bits, field, step);

                        EditorGUI.BeginDisabledGroup(true);
                        {
                            DrawPrecisionResult(layout.Precision, field, step, "°");
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                }

                GUIStyle miniText = new GUIStyle(EditorStyles.miniLabel);
                miniText.alignment = TextAnchor.MiddleRight;
                string bits = BindingsWindowSettings.CompactView ? " b" : (field.TotalBits == 1 ? " Bit" : " Bits");
                EditorGUI.LabelField(layout.BitTotal, $"{field.TotalBits}{bits}", miniText);

                if (basePrefabBindingArchetypeData is not null
                    && (basePrefabBindingArchetypeData.Fields.Count <= step
                        || !field.Equals(basePrefabBindingArchetypeData.GetLODstep(step)))
                    && (archetypeData.FloatCompression is not FloatCompression.None
                    || !IsFloatType()))
                {
                    DrawPropertyOverrideIndicator(rect);
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        protected void DrawIcon(Rect rect)
        {
            if (IsMethod)
            {
                GUI.DrawTexture(rect, ContentUtils.GUIContents.command.image);
            }
            else
            {
                var icon = ContentUtils.GetSchemaTypeIcon(archetypeData.SchemaType);
                GUI.DrawTexture(rect, icon.image);
            }

            RectOffset rectOffset = new RectOffset(2, 2, 2, 4);
            GUI.Label(rectOffset.Add(rect), ContentUtils.GUIContents.binding);
        }

        private void DrawIntBits(Rect rect, BindingLODStepData field)
        {
            int multiplier = ArchetypeMath.GetBitsMultiplier(field.SchemaType);

            GUIStyle miniText = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            miniText.alignment = TextAnchor.MiddleLeft;

            if (multiplier > 1)
            {
                Rect multiplierrect = new Rect(rect.x + 35, rect.y, 20, rect.height);
                GUI.Label(multiplierrect, $"*{multiplier}", miniText);
            }

            EditorGUI.LabelField(rect, $"{field.Bits} {(field.Bits == 1 ? "Bit" : "Bits")}", miniText);
        }

        private void DrawIntRangeInput(Rect minRect, Rect maxRect, int step)
        {
            EditorGUI.BeginChangeCheck();

            var newMin = BindingsTreeViewIntHelper.DrawField(minRect, archetypeData.MinRange, BindingsTreeViewInput.Type.MinRange,
                step, SelectedInTreeView, warningSource?.Active ?? false);

            if (EditorGUI.EndChangeCheck())
            {
                var edit = new BindingsTreeViewInput(step, BindingsTreeViewInput.Type.MinRange, newMin, "Changed MinRange");
                ApplyInput(edit);
            }
            EditorGUI.BeginChangeCheck();

            var newMax = BindingsTreeViewIntHelper.DrawField(maxRect, archetypeData.MaxRange, BindingsTreeViewInput.Type.MaxRange,
                step, SelectedInTreeView, warningSource?.Active ?? false);

            if (EditorGUI.EndChangeCheck())
            {
                var edit = new BindingsTreeViewInput(step, BindingsTreeViewInput.Type.MaxRange, newMax, "Changed MaxRange");
                ApplyInput(edit);
            }
        }

        private void DrawBasicBitsInput(Rect rect, BindingLODStepData field, int step)
        {
            EditorGUI.BeginChangeCheck();
            int bits = BindingsTreeViewFloatHelper.DrawBindingBitsInput(rect, field, step, SelectedInTreeView);

            if (EditorGUI.EndChangeCheck())
            {
                var edit = new BindingsTreeViewInput(step, BindingsTreeViewInput.Type.Bits, bits, "Changed Bits");
                ApplyInput(edit);
            }
        }

        private void DrawPrecisionResult(Rect rect, BindingLODStepData field, int step, string unit = "")
        {
            double precision = PrecisionPopupDrawer.Draw(rect, boundComponent, Binding, archetypeData, field, step, bindingsWindow, basePrefabBindingArchetypeData, unit);
            if (precision >= 0 && precision != field.Precision)
            {
                var edit = new BindingsTreeViewInput(step, BindingsTreeViewInput.Type.Precision, precision, "Changed Precision");
                ApplyInput(edit);
            }
        }

        protected virtual void DrawInterpolation(Rect rect)
        {
            if (!GetCanInterpolate())
            {
                return;
            }

            var idx = sync.Bindings.IndexOf(Binding);
            if (idx == -1)
            {
                return;
            }

            var path = $"bindings.Array.data[{idx}].{nameof(Binding.interpolationSettings)}";
            using var so = new SerializedObject(sync);
            using var p = so.FindProperty(path);
            _ = EditorGUI.PropertyField(rect, p);
        }

        protected void ApplyInput(BindingsTreeViewInput input) => OnInput?.Invoke(input, this);

        internal void ApplyInputToBinding(BindingsTreeViewInput input, bool saveToDisk)
        {
            input.ApplyToBinding(archetypeData, this);

            // TODO: change to use the SerializedObject
            if (sync)
            {
                var bindingIndex = sync.Bindings.FindIndex(x => x.guid == Binding.guid);
                if (bindingIndex is not -1)
                {
                    sync.Bindings[bindingIndex].BindingArchetypeData = archetypeData;
                }
                else
                {
                    Logger.Context = sync;
                    Logger.Warning(Warning.ConfigurationWindowWarning, $"Binding {Binding.guid} was not found on '{sync}' when trying to save changes to disk.");
                }
            }
            else
            {
                Logger.Warning(Warning.ConfigurationWindowWarning, $"Sync was null when trying to save changes made to binding {Binding.guid} to disk.");
            }

            if (saveToDisk)
            {
                bindingsWindow.UpdateSerialization();
            }
        }

        internal int GetBitsOfLOD(int step) => archetypeData.GetLODstep(step).TotalBits;
        internal override bool CheckIfFilteredOut(BindingsWindowTreeFilters filters, bool bindingCanBeEdited) => filters.FilterOutBinding(SchemaType, displayName);

        private void CreateBindingsCommandMenu(Rect rect)
        {
            if (!bindingsWindow.StateController.Lods)
            {
                return;
            }

            bool canResetRanges = IsRangedType();
            bool canResetBitsAndPrecision = IsFloatType() || BindingArchetypeData.IsBitsBased(SchemaType);

            if (canResetRanges || canResetBitsAndPrecision)
            {
                if (GUI.Button(rect, EditorGUIUtility.IconContent("_Menu"), EditorStyles.label))
                {
                    var menu = new GenericMenu();

                    if (canResetRanges && canResetBitsAndPrecision)
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent("Reset All"), false, () => ResetToAllToDefaultValues(BindingsTreeViewInput.BindingReset.All));
                    }

                    if (canResetRanges)
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent("Reset Ranges"), false, () => ResetToAllToDefaultValues(BindingsTreeViewInput.BindingReset.RangesOnly));
                    }

                    if (canResetBitsAndPrecision)
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent("Reset Bits and Precision"), false, () => ResetToAllToDefaultValues(BindingsTreeViewInput.BindingReset.BitsAndPrecisionOnly));
                    }

                    if ((archetypeData is not null && !archetypeData.Equals(basePrefabBindingArchetypeData))
                        || (basePrefabBindingToolkitArchetype is not null
                            && !sync.Archetype.LODLevels.Select(x => x.Distance)
                                .SequenceEqual(basePrefabBindingToolkitArchetype.LODLevels.Select(x => x.Distance))))
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent("Apply To Base Prefab"), false, ApplyOverridesToBasePrefab);
                        menu.AddItem(EditorGUIUtility.TrTextContent("Revert Prefab Overrides"), false, RevertPrefabOverrides);
                    }

                    if (HasPrefabVariants())
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent("Apply To All Prefab Variants"), false, ApplyToAllVariants);
                    }

                    menu.DropDown(rect);
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>
        /// Apply all changes done to this binding to the base prefab of the prefab variant containing this binding.
        /// </summary>
        private void ApplyOverridesToBasePrefab()
        {
            var basePrefab = PrefabUtils.FindBasePrefab(boundComponent.Component);
            if (!basePrefab)
            {
                Debug.LogWarning($"Could not find base prefab of '{boundComponent.Component}'.", BoundComponent.Component);
                return;
            }

            var basePrefabSync = basePrefab.GetComponentInParent<CoherenceSync>(includeInactive: true);
            if (!basePrefabSync)
            {
                Debug.LogWarning($"Could not find CoherenceSync on base prefab '{basePrefab.name}'.", basePrefab);
                return;
            }

            var basePrefabBoundComponent = basePrefabSync.Archetype.BoundComponents.FirstOrDefault(x => ReferenceEquals(x.Component, basePrefab));
            if (basePrefabBoundComponent is null)
            {
                Debug.LogWarning($"Could not find bound component '{boundComponent.Component.GetType().Name}' on base prefab '{basePrefab.name}'.", basePrefab);
                return;
            }

            var basePrefabBinding = basePrefabBoundComponent.Bindings.FirstOrDefault(x => x.guid == Binding.guid);
            if (basePrefabBinding is null)
            {
                Debug.LogWarning($"Could not find binding '{Binding.Name}' on component '{basePrefabBoundComponent.Component.GetType().Name}' on base prefab '{basePrefabSync.name}'.", basePrefab);
                return;
            }

            var guid = Binding.guid;
            var bindingIndex = basePrefabSync.Bindings.FindIndex(x => x.guid == guid);
            if (bindingIndex is -1)
            {
                return;
            }

            Undo.RecordObject(basePrefabSync, "Apply To Base Prefab");

            using var serializedObject = new SerializedObject(basePrefabSync);
            using var bindingsProperty = serializedObject.FindProperty(CoherenceSync.Property.bindings);
            using var bindingProperty = bindingsProperty.GetArrayElementAtIndex(bindingIndex);
            using var archeTypeDataProperty = bindingProperty.FindPropertyRelative(Binding.Property.archetypeData);

            using var typeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.type));
            typeProperty.intValue = (int)archetypeData.SchemaType;
            using var minRangeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.minRange));
            minRangeProperty.longValue = archetypeData.MinRange;
            using var maxRangeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.maxRange));
            maxRangeProperty.longValue = archetypeData.MaxRange;
            using var floatCompressionProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.floatCompression));
            floatCompressionProperty.intValue = (int)archetypeData.FloatCompression;
            using var sampleRateProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.sampleRate));
            sampleRateProperty.floatValue = archetypeData.SampleRate;

            using var fieldsProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.fields));
            if (fieldsProperty.arraySize < Binding.archetypeData.Fields.Count)
            {
                fieldsProperty.arraySize = Binding.archetypeData.Fields.Count;
            }

            for (var i = 0; i < Binding.archetypeData.Fields.Count; i++)
            {
                var lodStep = Binding.archetypeData.GetLODstep(i);
                using var lodStepProperty = fieldsProperty.GetArrayElementAtIndex(i);
                using var bitsProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.bits);
                bitsProperty.intValue = lodStep.Bits;
                using var precisionProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.precision);
                precisionProperty.doubleValue = lodStep.Precision;
                using var lodTypeProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.type);
                lodTypeProperty.intValue = (int)lodStep.SchemaType;
                using var lodFloatCompressionProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.floatCompression);
                lodFloatCompressionProperty.intValue = (int)lodStep.FloatCompression;
            }

            using var archetypeProperty = serializedObject.FindProperty(CoherenceSync.Property.archetype);
            using var lodLevelsProperty = archetypeProperty.FindPropertyRelative(ToolkitArchetype.Property.lodLevels);
            if (lodLevelsProperty.arraySize < sync.Archetype.LODLevels.Count)
            {
                lodLevelsProperty.arraySize = sync.Archetype.LODLevels.Count;
            }

            for (var i = 0; i < sync.Archetype.LODLevels.Count; i++)
            {
                using var lodLevelProperty = lodLevelsProperty.GetArrayElementAtIndex(i);
                using var distanceProperty = lodLevelProperty.FindPropertyRelative(ArchetypeLODStep.Property.distance);
                distanceProperty.floatValue = sync.Archetype.LODLevels[i].Distance;
            }

            using var boundComponentsProperty = archetypeProperty.FindPropertyRelative(ToolkitArchetype.Property.boundComponents);
            var boundComponentInBasePrefabIndex = basePrefabSync.Archetype.BoundComponents.FindIndex(x => ReferenceEquals(x.Component, basePrefab));
            if (boundComponentInBasePrefabIndex is not -1)
            {
                using var boundComponentProperty = boundComponentsProperty.GetArrayElementAtIndex(boundComponentInBasePrefabIndex);
                using var lodStepsActiveProperty = boundComponentProperty.FindPropertyRelative(ArchetypeComponent.Property.lodStepsActive);
                var setLodStepsActive = BoundComponent.LodStepsActive;
                if (lodStepsActiveProperty.intValue < setLodStepsActive)
                {
                    lodStepsActiveProperty.intValue = setLodStepsActive;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(basePrefabSync);
            PrefabUtility.SavePrefabAsset(basePrefabSync.gameObject);

            if (boundComponentInBasePrefabIndex is -1)
            {
                return;
            }

            var boundComponentInBasePrefab = basePrefabSync.Archetype.BoundComponents[boundComponentInBasePrefabIndex];
            var basePrefabBoundComponentBinding = boundComponentInBasePrefab.Bindings.FirstOrDefault(x => x.guid == Binding.guid);
            if (basePrefabBoundComponentBinding is null)
            {
                return;
            }

            basePrefabBindingArchetypeData.CopyFrom(archetypeData);
            basePrefabBinding.BindingArchetypeData = basePrefabBindingArchetypeData;

            Debug.Log($"Optimizations applied to base prefab {basePrefab.name}.", basePrefab);
        }

        /// <summary>
        /// Revert all changes done to this binding on the prefab variant containing the binding.
        /// </summary>
        private void RevertPrefabOverrides()
        {
            var basePrefab = PrefabUtils.FindBasePrefab(BoundComponent.Component);
            if (!basePrefab)
            {
                Debug.LogWarning($"Could not find base prefab of '{boundComponent.Component}'.", BoundComponent.Component);
                return;
            }

            var basePrefabSync = basePrefab.GetComponentInParent<CoherenceSync>(true);
            if (!basePrefabSync)
            {
                Debug.LogWarning($"Could not find CoherenceSync on base prefab '{basePrefab.name}'.", basePrefab);
                return;
            }

            var guid = Binding.guid;
            var bindingIndex = basePrefabSync.Bindings.FindIndex(x => x.guid == guid);
            if (bindingIndex is -1)
            {
                return;
            }

            Undo.RecordObject(sync, "Revert Prefab Overrides");

            var setLodSteps = basePrefabBindingArchetypeData.Fields.Count;
            using var serializedObject = new SerializedObject(sync);
            using var bindingsProperty = serializedObject.FindProperty(CoherenceSync.Property.bindings);
            using var bindingProperty = bindingsProperty.GetArrayElementAtIndex(bindingIndex);
            using var archeTypeDataProperty = bindingProperty.FindPropertyRelative(Binding.Property.archetypeData);

            using var typeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.type));
            typeProperty.intValue = (int)basePrefabBindingArchetypeData.SchemaType;
            using var minRangeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.minRange));
            minRangeProperty.longValue = basePrefabBindingArchetypeData.MinRange;
            using var maxRangeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.maxRange));
            maxRangeProperty.longValue = basePrefabBindingArchetypeData.MaxRange;
            using var floatCompressionProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.floatCompression));
            floatCompressionProperty.intValue = (int)basePrefabBindingArchetypeData.FloatCompression;
            using var sampleRateProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.sampleRate));
            sampleRateProperty.floatValue = basePrefabBindingArchetypeData.SampleRate;

            using var fieldsProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.fields));
            if (fieldsProperty.arraySize < basePrefabBindingArchetypeData.Fields.Count)
            {
                fieldsProperty.arraySize = basePrefabBindingArchetypeData.Fields.Count;
            }

            for (var i = 0; i < setLodSteps; i++)
            {
                var lodStep = basePrefabBindingArchetypeData.GetLODstep(i);
                using var lodStepProperty = fieldsProperty.GetArrayElementAtIndex(i);
                using var bitsProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.bits);
                bitsProperty.intValue = lodStep.Bits;
                using var precisionProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.precision);
                precisionProperty.doubleValue = lodStep.Precision;
                using var lodTypeProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.type);
                lodTypeProperty.intValue = (int)lodStep.SchemaType;
                using var lodFloatCompressionProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.floatCompression);
                lodFloatCompressionProperty.intValue = (int)lodStep.FloatCompression;
            }

            using var archetypeProperty = serializedObject.FindProperty(CoherenceSync.Property.archetype);
            using var lodLevelsProperty = archetypeProperty.FindPropertyRelative(ToolkitArchetype.Property.lodLevels);
            if (lodLevelsProperty.arraySize < basePrefabSync.Archetype.LODLevels.Count)
            {
                lodLevelsProperty.arraySize = basePrefabSync.Archetype.LODLevels.Count;
            }

            for (var i = 0; i < basePrefabSync.Archetype.LODLevels.Count; i++)
            {
                using var lodLevelProperty = lodLevelsProperty.GetArrayElementAtIndex(i);
                using var distanceProperty = lodLevelProperty.FindPropertyRelative(ArchetypeLODStep.Property.distance);
                distanceProperty.floatValue = basePrefabSync.Archetype.LODLevels[i].Distance;
            }

            using var boundComponentsProperty = archetypeProperty.FindPropertyRelative(ToolkitArchetype.Property.boundComponents);
            var boundComponentInBasePrefabIndex = basePrefabSync.Archetype.BoundComponents.FindIndex(x => ReferenceEquals(x.Component, basePrefab));
            if (boundComponentInBasePrefabIndex is not -1)
            {
                using var boundComponentProperty = boundComponentsProperty.GetArrayElementAtIndex(boundComponentInBasePrefabIndex);
                using var lodStepsActiveProperty = boundComponentProperty.FindPropertyRelative(ArchetypeComponent.Property.lodStepsActive);
                var setLodStepsActive = basePrefabSync.Archetype.BoundComponents[boundComponentInBasePrefabIndex].LodStepsActive;
                if (lodStepsActiveProperty.intValue < setLodStepsActive)
                {
                    lodStepsActiveProperty.intValue = setLodStepsActive;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(sync);
            PrefabUtility.SavePrefabAsset(sync.gameObject);

            if (boundComponentInBasePrefabIndex is -1)
            {
                return;
            }

            var boundComponentInBasePrefab = basePrefabSync.Archetype.BoundComponents[boundComponentInBasePrefabIndex];
            var basePrefabBoundComponentBinding = boundComponentInBasePrefab.Bindings.FirstOrDefault(x => x.guid == Binding.guid);
            if (basePrefabBoundComponentBinding is null)
            {
                return;
            }

            archetypeData.CopyFrom(basePrefabBoundComponentBinding.BindingArchetypeData);

            Debug.Log($"Optimizations made to binding '{Binding}' have been reverted.", BoundComponent.Component);

            bindingsWindow.OnPropertyValueChanged();
        }

        /// <summary>
        /// Apply all changes done to this binding to all prefab variants of the prefab containing this binding.
        /// </summary>
        private void ApplyToAllVariants()
        {
            var variants = GetAllPrefabVariants().ToArray();
            var variantCount = variants.Length;

            string title, message;
            if (variantCount == 1)
            {
                title = $"Apply to {variants[0].name}?";
                message = $"Apply {ObjectNames.NicifyVariableName(Binding.Name)}'s optimizations from '{sync.name}' into '{variants[0].name}'?";
            }
            else
            {
                title = $"Apply to {variants.Length} variants?";
                message = $"Apply {ObjectNames.NicifyVariableName(Binding.Name)}'s optimizations from '{sync.name}' into {variants.Length} prefab variants?\n\n";
                if (variantCount <= 10)
                {
                    message += string.Join("\n", variants.Select(x => x.name));
                }
                else
                {
                    message += string.Join("\n", variants.Take(10).Select(x => x.name));
                    message += "\n...";
                }
            }

            if (!EditorUtility.DisplayDialog(title, message, "Apply", "Cancel"))
            {
                return;
            }

            var appliedToCount = 0;

            Undo.RecordObjects(variants.Cast<Object>().ToArray(), "Apply To All Variants");

            foreach (var variant in variants)
            {
                if (!variant)
                {
                    continue;
                }

                var variantSync = variant.GetComponentInParent<CoherenceSync>(true);
                if (!variantSync)
                {
                    continue;
                }

                var guid = Binding.guid;
                var bindingIndex = variantSync.Bindings.FindIndex(x => x.guid == guid);
                if (bindingIndex is -1)
                {
                    continue;
                }

                using var serializedObject = new SerializedObject(variantSync);
                using var bindingsProperty = serializedObject.FindProperty(CoherenceSync.Property.bindings);
                using var bindingProperty = bindingsProperty.GetArrayElementAtIndex(bindingIndex);
                using var archeTypeDataProperty = bindingProperty.FindPropertyRelative(Binding.Property.archetypeData);

                using var typeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.type));
                typeProperty.intValue = (int)archetypeData.SchemaType;
                using var minRangeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.minRange));
                minRangeProperty.longValue = archetypeData.MinRange;
                using var maxRangeProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.maxRange));
                maxRangeProperty.longValue = archetypeData.MaxRange;
                using var floatCompressionProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.floatCompression));
                floatCompressionProperty.intValue = (int)archetypeData.FloatCompression;
                using var sampleRateProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.sampleRate));
                sampleRateProperty.floatValue = archetypeData.SampleRate;

                using var fieldsProperty = archeTypeDataProperty.FindPropertyRelative(nameof(BindingArchetypeData.Property.fields));
                if (fieldsProperty.arraySize < Lodsteps)
                {
                    fieldsProperty.arraySize = Lodsteps;
                }

                for (var i = 0; i < Lodsteps; i++)
                {
                    var lodStep = archetypeData.GetLODstep(i);
                    using var lodStepProperty = fieldsProperty.GetArrayElementAtIndex(i);
                    using var bitsProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.bits);
                    bitsProperty.intValue = lodStep.Bits;
                    using var precisionProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.precision);
                    precisionProperty.doubleValue = lodStep.Precision;
                    using var lodTypeProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.type);
                    lodTypeProperty.intValue = (int)lodStep.SchemaType;
                    using var lodFloatCompressionProperty = lodStepProperty.FindPropertyRelative(BindingLODStepData.Property.floatCompression);
                    lodFloatCompressionProperty.intValue = (int)lodStep.FloatCompression;
                }

                using var archetypeProperty = serializedObject.FindProperty(CoherenceSync.Property.archetype);
                using var lodLevelsProperty = archetypeProperty.FindPropertyRelative(ToolkitArchetype.Property.lodLevels);
                if (lodLevelsProperty.arraySize < sync.Archetype.LODLevels.Count)
                {
                    lodLevelsProperty.arraySize = sync.Archetype.LODLevels.Count;
                }

                for (var i = 0; i < sync.Archetype.LODLevels.Count; i++)
                {
                    using var lodLevelProperty = lodLevelsProperty.GetArrayElementAtIndex(i);
                    using var distanceProperty = lodLevelProperty.FindPropertyRelative(ArchetypeLODStep.Property.distance);
                    distanceProperty.floatValue = sync.Archetype.LODLevels[i].Distance;
                }

                using var boundComponentsProperty = archetypeProperty.FindPropertyRelative(ToolkitArchetype.Property.boundComponents);
                var boundComponentInPrefabVariantIndex = variantSync.Archetype.BoundComponents.FindIndex(x => ReferenceEquals(x.Component, variant));
                if (boundComponentInPrefabVariantIndex is not -1)
                {
                    using var boundComponentProperty = boundComponentsProperty.GetArrayElementAtIndex(boundComponentInPrefabVariantIndex);
                    using var lodStepsActiveProperty = boundComponentProperty.FindPropertyRelative(ArchetypeComponent.Property.lodStepsActive);
                    if (lodStepsActiveProperty.intValue < boundComponent.LodStepsActive)
                    {
                        lodStepsActiveProperty.intValue = boundComponent.LodStepsActive;
                    }
                }

                if (serializedObject.ApplyModifiedProperties())
                {
                    BakeUtil.CoherenceSyncSchemasDirty = true;
                }
                EditorUtility.SetDirty(variantSync);
                PrefabUtility.SavePrefabAsset(variantSync.gameObject);

                if (boundComponentInPrefabVariantIndex is -1)
                {
                    continue;
                }

                var boundComponentInPrefabVariant = variantSync.Archetype.BoundComponents[boundComponentInPrefabVariantIndex];
                var variantBoundComponentBinding = boundComponentInPrefabVariant.Bindings.FirstOrDefault(x => x.guid == Binding.guid);
                if (variantBoundComponentBinding is null)
                {
                    continue;
                }

                variantBoundComponentBinding.BindingArchetypeData.CopyFrom(archetypeData);
                appliedToCount++;
            }

            Debug.Log($"Optimizations for '{Binding.Name}' applied to {appliedToCount}/{variants.Length} variants:\n{string.Join("\n", variants.Select(x => x.name))}");
        }

        internal void ResetToAllToDefaultValues(BindingsTreeViewInput.BindingReset bindingReset)
        {
            var edit = new BindingsTreeViewInput(-1, BindingsTreeViewInput.Type.Reset, bindingReset, "Reset bindings");
            ApplyInput(edit);
        }

        internal void ResetValuesToDefault(bool resetRanges, bool resetBitsAndPrecision)
        {
            archetypeData.ResetValuesToDefault(Binding.MonoAssemblyRuntimeType, resetRanges, resetBitsAndPrecision);
        }

        internal override bool CanChangeExpandedState() => false;

        private bool IsRangedType() => archetypeData.IsRangeType();

        internal bool IsIntType()
        {
            return SchemaType == SchemaType.Int ||
                SchemaType == SchemaType.UInt ||
                SchemaType == SchemaType.Int64 ||
                SchemaType == SchemaType.UInt64 ||
                SchemaType == SchemaType.Int8 ||
                SchemaType == SchemaType.UInt8 ||
                SchemaType == SchemaType.Int16 ||
                SchemaType == SchemaType.UInt16 ||
                SchemaType == SchemaType.Char;
        }

        private bool IsFloatType() => archetypeData.IsFloatType;

        internal void SetGlobalWarning(string message)
        {
            warningSource = new WarningSource(message);
            bindingsWindow.SetWarning(warningSource.Token);
        }

        internal void ClearGlobalWarning() => bindingsWindow.ClearWarning();

        private bool HasWarnings()
        {
            warningList.Clear();

            if (archetypeData.SchemaType == SchemaType.Quaternion)
            {
                for (int i = 0; i < boundComponent.LodStepsActive; i++)
                {
                    var lodStep = archetypeData.GetLODstep(i);
                    if (lodStep.Bits < 12)
                    {
                        warningList.Add($"LOD {i} uses less than 12 bits ({lodStep.Bits}) per component. Due to the way quaternions are serialized this " +
                                        $"may severely affect quality of the quaternion syncing and is not recommended.");
                    }
                }
            }

            return warningList.Count > 0;
        }

        private void FixAllWarnings()
        {
            if (archetypeData.SchemaType == SchemaType.Quaternion)
            {
                for (int i = 0; i < boundComponent.LodStepsActive; i++)
                {
                    var lodStep = archetypeData.GetLODstep(i);
                    if (lodStep.Bits < 12)
                    {
                        ApplyInput(new BindingsTreeViewInput(i, BindingsTreeViewInput.Type.Bits, 12, "Fixed quaternion bits"));
                    }
                }
            }
            bindingsWindow.UpdateSerialization();
            warningList.Clear();
        }

        private bool HasPrefabVariants() => GetAllPrefabVariants().Any();

        private IEnumerable<Component> GetAllPrefabVariants()
        {
            var component = boundComponent.Component;
            if (!component)
            {
                return Array.Empty<Component>();
            }

            var allNetworkedPrefabs = CoherenceSyncConfigRegistry.Instance
                .Select(config =>
                {
                    var configSync = config?.Sync;
                    return configSync ? configSync.gameObject : null;
                })
                .Where(go => go is not null);

            return PrefabUtils.FindInAllVariants(component, allNetworkedPrefabs);
        }

        internal static void DrawPropertyOverrideIndicator(Rect rect) => EditorGUI.DrawRect(new(rect.x - 4f, rect.y, 2f, rect.height - 1f), PropertyOverrideColor);
    }
}
