namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using System;
    using Coherence.Toolkit.Debugging;
    using UnityEngine;

    [Serializable]
    public class ShortBinding : ValueBinding<short>
    {
        protected ShortBinding() { }
        public ShortBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override short Value
        {
            get => (short)GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override bool DiffersFrom(short first, short second)
        {
            return first != second;
        }

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher() => ToggleInterpolationDebugGrapher(v => v);
    }
}
