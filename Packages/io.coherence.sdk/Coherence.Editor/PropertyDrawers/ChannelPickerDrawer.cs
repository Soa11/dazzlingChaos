// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Linq;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(ChannelPickerAttribute))]
    internal class ChannelPickerDrawer : PropertyDrawer
    {
        private static readonly GUIContent nonExistingWarn = EditorGUIUtility.TrIconContent("Warning", "The selected channel doesn't exist " +
                        "and the entity will not be synchronized. Please select another channel.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var options = Channel.GetAll().Where(c => c.SupportsEntitySyncing).ToArray();

            _ = EditorGUI.BeginProperty(position, label, property);
            {
                position = EditorGUI.PrefixLabel(position, label);

                var currentChannelName = property.stringValue;
                var currentIndex = Array.FindIndex(options, options => options.Name == currentChannelName);

                if (currentIndex == -1)
                {
                    var warnRect = position;
                    warnRect.width = 18;

                    GUI.Label(warnRect, nonExistingWarn, EditorStyles.label);

                    position.x += 20;
                    position.width -= 20;
                }

                var newIndex = EditorGUI.Popup(
                    position,
                    currentIndex,
                    options.Select(c => new GUIContent(c.Name, c.Description)).ToArray());

                if (newIndex != currentIndex)
                {
                    property.stringValue = options[newIndex].Name;
                }
            }
            EditorGUI.EndProperty();
        }
    }
}
