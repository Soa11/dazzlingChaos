namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using System;
    using Coherence.Toolkit.Debugging;
    using UnityEngine;

    [Serializable]
    public class ByteBinding : ValueBinding<byte>
    {
        protected ByteBinding() { }
        public ByteBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override byte Value
        {
            get => (byte)GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override bool DiffersFrom(byte first, byte second)
        {
            return first != second;
        }

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher() => ToggleInterpolationDebugGrapher(v => v);
    }
}
