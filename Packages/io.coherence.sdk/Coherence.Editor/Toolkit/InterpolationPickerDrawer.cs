// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Bindings;
    using Interpolation;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [CustomPropertyDrawer(typeof(InterpolationPickerAttribute))]
    internal class InterpolationPickerDrawer : ObjectPickerDrawer
    {
        protected override GUIContent GetIconContent(SerializedProperty property)
        {
            var obj = property.objectReferenceValue as InterpolationSettings;
            return ContentUtils.GetInterpolationContent(obj);
        }

        protected override Object CreateInstance()
        {
            return InterpolationSettings.CreateDefault();
        }

        protected override void OnReferenceChanging(Object oldReference, Object newReference)
        {
            // On this method, we want to know if interpolation setting has changed from/to None.
            // We do this to dirty the schemas / require a bake.
            // We only care about it happening on CoherenceSync components (drawer could be used anywhere else).

            if (!objs.Any(o => o is CoherenceSync))
            {
                return;
            }

            if (!path.StartsWith("bindings.Array", StringComparison.InvariantCulture))
            {
                return;
            }

            var oldIsNone = (oldReference is InterpolationSettings { IsInterpolationNone: true } || !oldReference);
            var newIsNone = (newReference is InterpolationSettings { IsInterpolationNone: true } || !newReference);

            if (oldIsNone != newIsNone)
            {
                BakeUtil.CoherenceSyncSchemasDirty = true;
            }
        }

        protected override void OnReferenceChanged(Object oldReference, Object newReference)
        {
            var oldSettings = oldReference as InterpolationSettings;
            var newSettings = newReference as InterpolationSettings;

            var attr = (InterpolationPickerAttribute)attribute;
            var sync = (CoherenceSync)cachedProperty.serializedObject.targetObject;
            var binding = GetBindingByPropertyPath(sync, cachedProperty.propertyPath);

            var callback = FindMethod(binding.GetType(), attr.onChangedCallback,
                new Type[] { typeof(InterpolationSettings), typeof(InterpolationSettings) });

            if (callback == null)
            {
                throw new Exception($"Couldn't find instance method {attr.onChangedCallback} with exact parameters to invoke on interpolation settings change.");
            }

            _ = callback.Invoke(binding, new object[] { oldSettings, newSettings });
        }

        private MethodInfo FindMethod(Type type, string methodName, Type[] parameterTypes)
        {
            while (type != null)
            {
                var method = type.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    parameterTypes,
                    null);

                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private Binding GetBindingByPropertyPath(CoherenceSync sync, string propertyPath)
        {
            if (!propertyPath.StartsWith("bindings.Array.data["))
            {
                throw new ArgumentException("Invalid property path");
            }

            var startIndex = "bindings.Array.data[".Length;
            var endIndex = propertyPath.IndexOf(']', startIndex);
            var indexString = propertyPath.Substring(startIndex, endIndex - startIndex);
            if (!int.TryParse(indexString, out var index))
            {
                throw new ArgumentException("Invalid index in property path");
            }

            return sync.Bindings[index];
        }
    }
}
