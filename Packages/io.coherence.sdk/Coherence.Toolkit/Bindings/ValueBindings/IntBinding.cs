namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using System;
    using Coherence.Toolkit.Debugging;
    using UnityEngine;

    [Serializable]
    public class IntBinding : ValueBinding<int>
    {
        protected IntBinding() { }
        public IntBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override int Value
        {
            get => (int)GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override int ClampToRange(in int value, long minRange, long maxRange)
        {
            if (value < minRange) return (int)minRange;
            if (value > maxRange) return (int)maxRange;
            return value;
        }

        protected override bool DiffersFrom(int first, int second)
        {
            return first != second;
        }

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher() => ToggleInterpolationDebugGrapher(v => v);
    }
}
