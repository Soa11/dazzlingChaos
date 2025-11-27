// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    internal static class ComponentUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MaybeNull]
        public static T SelfOrNull<T>([MaybeNull] this T self) where T : Object => self ? self : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MaybeNull]
        public static Transform TransformOrNull<T>([MaybeNull] this T self) where T : Component => self ? self.transform : null;
    }
}
