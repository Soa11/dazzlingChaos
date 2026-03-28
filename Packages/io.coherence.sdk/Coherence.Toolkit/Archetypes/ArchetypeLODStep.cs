// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using UnityEngine;

    [Serializable]
    internal class ArchetypeLODStep : IEquatable<ArchetypeLODStep>
    {
        public float Distance => distance;
        [SerializeField] private float distance;

        public ArchetypeLODStep() => distance = float.MaxValue;
        public void SetDistance(float newDistance) => distance = newDistance;
        public ArchetypeLODStep(ArchetypeLODStep other) => distance = other.distance;

        public bool Equals(ArchetypeLODStep other) => other is not null && distance.Equals(other.distance);
        public override bool Equals(object obj) => obj is ArchetypeLODStep other && Equals(other);
        public override int GetHashCode() => distance.GetHashCode();

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties found in <see cref="ArchetypeLODStep"/>.
        /// Can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string distance = nameof(ArchetypeLODStep.distance);
        }
#endif
    }
}
