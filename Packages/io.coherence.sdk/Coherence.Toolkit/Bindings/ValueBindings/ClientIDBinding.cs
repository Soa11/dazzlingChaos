namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using System;
    using UnityEngine;
    using Connection;
    using Coherence.Toolkit.Debugging;

    [Serializable]
    public class ClientIDBinding : ValueBinding<ClientID>
    {
        protected ClientIDBinding() { }
        public ClientIDBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override ClientID Value
        {
            get => (ClientID)GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override bool DiffersFrom(ClientID first, ClientID second)
        {
            return first != second;
        }

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher() => ToggleInterpolationDebugGrapher(v => (uint)v);
    }
}
