// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Coherence.Toolkit.Bindings;
    using System.Linq;
    using Coherence.Toolkit;
    using UnityEngine;
    using Utils;
    using Log;
    using Logger = Log.Logger;

    public class DescriptorProvider
    {
        private readonly Logger logger = Log.GetLogger<DescriptorProvider>();

        // Storing reference as GameObject to not trigger Unity 2020 bug
        // that leads to null references after deserializing CoherenceSync
        // references after editor startup.
        public GameObject Root { get; internal set; }

        public Component Component { get; private set; }

        public virtual bool IsRootComponent => Component.gameObject.transform.parent == null ||
                                               Component.gameObject.transform.parent.name
                                                   .Equals("Canvas (Environment)");

        public virtual bool EmitSchemaComponentDefinition => true;
        public virtual string SchemaComponentNameOverride => null;

        public virtual bool AssociateCoherenceComponentTypePerBinding => false;

        public virtual bool EmitMonoComponentReferenceOnBakedSyncScript => true;
        public virtual string MonoComponentReferenceFieldNameOverride => null;
        public virtual Type MonoComponentReferenceTypeOverride => null;

        private static readonly Dictionary<Type, List<Descriptor>> descriptorCache = new();

        internal void SetComponent(Component component) => Component = component;

        protected virtual bool CustomFieldFilter(FieldInfo fieldInfo) => true;

        protected virtual bool CustomPropertyFilter(PropertyInfo propertyInfo) => true;

        protected virtual bool CustomMethodFilter(MethodInfo methodInfo) => true;

        public virtual List<Descriptor> Fetch()
        {
            if (!descriptorCache.TryGetValue(Component.GetType(), out var cachedDescriptors))
            {
                cachedDescriptors = GetDescriptorsUsingReflection();
                descriptorCache.Add(Component.GetType(), cachedDescriptors);
            }

            return new List<Descriptor>(cachedDescriptors);
        }

        internal void ClearDescriptorCache() => descriptorCache.Clear();

        private List<Descriptor> GetDescriptorsUsingReflection()
        {
            var descriptors = new List<Descriptor>();
            var componentType = Component.GetType();

            // Fields
            descriptors.AddRange(componentType
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => IsFieldBindable(field) && CustomFieldFilter(field))
                .Select(field => new Descriptor(componentType, field)));

            var interfaceCache = new InterfaceCache();

            // Properties
            descriptors.AddRange(componentType
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(property => IsPropertyBindable(property, interfaceCache) && property.IsValidBinding() &&
                                   CustomPropertyFilter(property))
                .Select(property => new Descriptor(componentType, property)));

            for (var baseType = componentType.BaseType;
                 !TypeUtils.BaseTypesAndNull.Contains(baseType);
                 baseType = baseType.BaseType)
            {
                foreach (var property in baseType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance |
                                                                BindingFlags.DeclaredOnly))
                {
                    if (ReflectionUtils.IsPubliclyAccessible(property, interfaceCache))
                    {
                        descriptors.Add(new(baseType, property));
                    }
                }
            }

            // Methods
            descriptors.AddRange(componentType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => IsMethodBindable(method, interfaceCache) && CustomMethodFilter(method))
                .Select(method => new Descriptor(componentType, method)));

            for (var baseType = componentType.BaseType;
                 !TypeUtils.BaseTypesAndNull.Contains(baseType);
                 baseType = baseType.BaseType)
            {
                foreach (var method in baseType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance |
                                                           BindingFlags.DeclaredOnly))
                {
                    if (ReflectionUtils.IsPubliclyAccessible(method, interfaceCache))
                    {
                        descriptors.Add(new(baseType, method));
                    }
                }
            }

            return descriptors;
        }

        private bool IsFieldBindable(FieldInfo fieldInfo)
        {
            var bindingState = fieldInfo.IsValidBinding() ? BindingState.Valid : BindingState.Incompatible;
            var hasSync = fieldInfo.IsDefined(typeof(SyncAttribute));

            if (!fieldInfo.IsPublic)
            {
                bindingState = BindingState.Private;
            }

            if (bindingState != BindingState.Private || !hasSync)
            {
                return bindingState == BindingState.Valid;
            }

            // DeclaringType can be null. Using ?? instead of putting ?. everywhere
            var type = fieldInfo.DeclaringType ?? typeof(object);
            logger.Error(Error.ToolkitBindingUnsupported,
                $"Field {type.Name}.{fieldInfo.Name} is private.");

            return false;
        }

        private bool IsPropertyBindable(PropertyInfo propertyInfo, InterfaceCache interfaceCache)
        {
            var bindingState = BindingState.Valid;
            var hasSync = propertyInfo.IsDefined(typeof(SyncAttribute), true);

            if (!ReflectionUtils.IsPubliclyAccessible(propertyInfo, interfaceCache))
            {
                bindingState = BindingState.Private;
            }

            if (bindingState != BindingState.Private || !hasSync)
            {
                return bindingState == BindingState.Valid;
            }

            // DeclaringType can be null. Using ?? instead of putting ?. everywhere
            var type = propertyInfo.DeclaringType ?? typeof(object);
            logger.Error(Error.ToolkitBindingUnsupported,
                $"Property {type.Name}.{propertyInfo.Name} is private.");

            return false;
        }

        private bool IsMethodBindable(MethodInfo methodInfo, InterfaceCache interfaceCache)
        {
            var bindingState = methodInfo.GetBindingState();
            var isPubliclyAccessible =
                ReflectionUtils.IsPubliclyAccessible(methodInfo, interfaceCache) || methodInfo.IsPublic;
            if (!methodInfo.IsDefined(typeof(CommandAttribute)))
            {
                return bindingState == BindingState.Valid && isPubliclyAccessible;
            }

            if (bindingState == BindingState.Valid && !isPubliclyAccessible)
            {
                bindingState = BindingState.Private;
            }

            // DeclaringType can be null. Using ?? instead of putting ?. everywhere
            var type = methodInfo.DeclaringType ?? typeof(object);

            switch (bindingState)
            {
                case BindingState.Incompatible:
                    logger.Error(Error.ToolkitBindingIncompatible,
                        $"Method {type.Name}.{methodInfo.Name} cannot return a value and cannot contain unsupported parameters");
                    break;
                case BindingState.Obsolete:
                    logger.Warning(Warning.ToolkitBindingObsolete,
                        $"Method {type.Name}.{methodInfo.Name} is obsolete");
                    break;
                case BindingState.SpecialName:
                    logger.Error(Error.ToolkitBindingSpecial,
                        $"Method {type.Name}.{methodInfo.Name} is a special name. Please remove the [Command] attribute");
                    break;
                case BindingState.UnsupportedType:
                    logger.Error(Error.ToolkitBindingUnsupported,
                        $"Command bindings cannot target members of the type {type.Name}");
                    break;
                case BindingState.Private:
                    logger.Error(Error.ToolkitBindingUnsupported,
                        $"Method {type.Name}.{methodInfo.Name} is private. Command bindings can only target public methods");
                    break;
            }

            return bindingState == BindingState.Valid || bindingState == BindingState.Obsolete;
        }

        public virtual GUIContent GetIconContent(Descriptor descriptor) => GUIContent.none;

        public virtual MenuItemData[] AdditionalMenuItemData => null;
    }
}
