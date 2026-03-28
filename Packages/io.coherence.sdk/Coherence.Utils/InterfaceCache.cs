// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    internal sealed class InterfaceCache
    {
        private readonly Dictionary<Type, Type[]> interfaces = new();
        private readonly Dictionary<(Type, Type), InterfaceMapping> interfaceMappings = new();

        public Type[] GetInterfaces(Type type)
        {
            if (!interfaces.TryGetValue(type, out var result))
            {
                result = type.GetInterfaces();
                interfaces[type] = result;
            }

            return result;
        }

        public InterfaceMapping GetInterfaceMap(Type implementingType, Type interfaceType)
        {
            var key = (type: implementingType, interfaceType);
            if (!interfaceMappings.TryGetValue(key, out var result))
            {
                result = implementingType.GetInterfaceMap(interfaceType);
                interfaceMappings[key] = result;
            }

            return result;
        }
    }
}
