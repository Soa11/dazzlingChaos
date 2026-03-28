namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using System;
    using UnityEngine;
    using Utils;
    using SimulationFrame;
    using Coherence.Toolkit.Debugging;

    [Serializable]
    public class ByteArrayBinding : ValueBinding<byte[]>
    {
        protected ByteArrayBinding() { }
        public ByteArrayBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override byte[] Value
        {
            get => (byte[])GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override bool DiffersFrom(byte[] first, byte[] second)
        {
            return first.DiffersFrom(second);
        }

        public override void IsDirty(AbsoluteSimulationFrame simulationFrame, out bool dirty, out bool justStopped)
        {
            if (SyncMode == SyncMode.Manual)
            {
                if (forceSync)
                {
                    UpdateLastCheckedForDirty(Value);
                }

                justStopped = forceSync;
                dirty = forceSync;
                forceSync = false;
                return;
            }

            justStopped = false;

            if (forceSync)
            {
                UpdateLastCheckedForDirty(Value);

                forceSync = false;
                dirty = true;
                return;
            }

            var value = Value;

            var changed = DiffersFrom(value, lastCheckedForDirty);
            if (!changed)
            {
                dirty = false;
                return;
            }

            UpdateLastCheckedForDirty(value);

            forceSync = false;
            dirty = true;
        }

        private void UpdateLastCheckedForDirty(byte[] value)
        {
            if (value != null)
            {
                if (lastCheckedForDirty == null || value.Length != lastCheckedForDirty.Length)
                {
                    lastCheckedForDirty = new byte[value.Length];
                }

                value.CopyTo(lastCheckedForDirty, 0);
            }
            else
            {
                lastCheckedForDirty = null;
            }
        }

        [InterpolationDebugContextItem("length")]
        public void ToggleInterpolationDebugGrapherForLength() => ToggleInterpolationDebugGrapher(v => v?.Length ?? 0, "length");
    }
}
