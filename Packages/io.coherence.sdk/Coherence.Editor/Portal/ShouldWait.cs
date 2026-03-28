// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using UnityEngine;

    internal enum ShouldWait
    {
        Never = 0,
        InBatchMode = 1,
        Always = 2
    }

    internal static class ShouldWaitExtensions
    {
        public static bool ShouldWait(this ShouldWait shouldWait)
            => shouldWait is Editor.ShouldWait.Always || (shouldWait is Editor.ShouldWait.InBatchMode && Application.isBatchMode);
    }
}
