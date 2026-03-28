// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Debugging
{
    using System;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal class InterpolationDebugContextItemAttribute : Attribute
    {
        public string Name { get; private set; }

        public InterpolationDebugContextItemAttribute(string name = null)
        {
            Name = name;
        }
    }
}
