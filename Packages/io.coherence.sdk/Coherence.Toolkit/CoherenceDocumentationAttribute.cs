// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;

    /// <summary>
    /// Associates a class with a documentation key.
    /// </summary>
    /// <remarks>
    /// Helps inject the coherence documentation URL associated with the <see cref="DocumentationKey"/> into the <see cref="UnityEngine.HelpURLAttribute"/> found on the same component.
    /// This injection is done through the Development Settings section in the Coherence Settings window.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    internal class CoherenceDocumentationAttribute : Attribute
    {
        public DocumentationKeys DocumentationKey { get; }
        internal CoherenceDocumentationAttribute(DocumentationKeys documentationKey)
        {
            DocumentationKey = documentationKey;
        }
    }
}
