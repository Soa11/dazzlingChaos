// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Bindings
{
    /// <summary>
    /// SyncMode defines when a <see cref="ValueBinding{T}"/> is replicated.
    /// </summary>
    public enum SyncMode
    {
        /// <summary>
        /// The binding is always replicated.
        /// </summary>
        Always,

        /// <summary>
        /// The binding is only replicated when the network object is created.
        /// </summary>
        CreationOnly,

        /// <summary>
        /// The binding is only replicated when manually requested.
        /// </summary>
        /// <remarks>
        /// It is advised to disable interpolation on bindings with this sync mode as it can
        /// result in visual artifacts if the value is changed irregularly.
        /// </remarks>
        Manual
    }
}
