// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using UnityEngine;

    /// <summary>
    /// Extension methods for <see cref="GUILayoutOption"/>.
    /// </summary>
    internal static class GUILayoutOptionExtensions
    {
        private static readonly GUILayoutOption[] SingleOptionArray = new GUILayoutOption[1];
        private static readonly GUILayoutOption[] TwoOptionsArray = new GUILayoutOption[2];

        /// <summary>
        /// Converts a single <see cref="GUILayoutOption"/> into a temporary array that can be passed
        /// to methods expecting an array of <see cref="GUILayoutOption"/> without generating garbage.
        /// </summary>
        public static GUILayoutOption[] AsTempArray(this GUILayoutOption option)
        {
            SingleOptionArray[0] = option;
            return SingleOptionArray;
        }

        /// <summary>
        /// Converts two <see cref="GUILayoutOption">GUILayoutOptions</see> into a temporary array that can be passed
        /// to methods expecting an array of <see cref="GUILayoutOption"/> without generating garbage.
        /// </summary>
        public static GUILayoutOption[] AsTempArray(this GUILayoutOption firstOption, GUILayoutOption secondOption)
        {
            TwoOptionsArray[0] = firstOption;
            TwoOptionsArray[1] = secondOption;
            return TwoOptionsArray;
        }
    }
}
