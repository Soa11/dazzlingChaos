namespace Coherence.Toolkit.Bindings.TransformBindings
{
    using Interpolation;
    using System;
    using UnityEngine;
    using ValueBindings;

    [Serializable]
    public class ScaleBinding : Vector3Binding
    {
        public override string CoherenceComponentName => "GenericScale";
        public override string MemberNameInComponentData => "value";
        public override string MemberNameInUnityComponent => nameof(CoherenceSync.coherenceLocalScale);
        public override string BakedSyncScriptGetter => nameof(CoherenceSync.coherenceLocalScale);
        public override string BakedSyncScriptSetter => nameof(CoherenceSync.coherenceLocalScale);

        protected ScaleBinding() { }

        public ScaleBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent) { }

        public override Vector3 Value
        {
            get => coherenceSync.coherenceLocalScale;
            set => coherenceSync.coherenceLocalScale = value;
        }

        public void ScaleSamples(Vector3 factor, bool transformLastSampleToo)
        {
            static Vector3 ScaleValue(Vector3 value, Vector3 factor) => Vector3.Scale(value, factor);
            static Sample<Vector3> ScaleSample(Sample<Vector3> sample, Vector3 factor) => new(ScaleValue(sample.Value, factor), sample.Stopped, sample.Time, sample.Latency);

            var buffer = Interpolator.Buffer;

            var count = (transformLastSampleToo ? buffer.Count : buffer.Count - 1);

            for (var index = 0; index < count; index++)
            {
                buffer[index] = ScaleSample(buffer[index], factor);
            }

            if (buffer.VirtualSamples.HasValue)
            {
                var (virtual1, virtual2) = buffer.VirtualSamples.Value;

                var newVirtual1 = ScaleSample(virtual1, factor);
                Sample<Vector3> newVirtual2;

                if (!transformLastSampleToo && buffer.Last.HasValue && virtual2.Time >= buffer.Last.Value.Time)
                {
                    newVirtual2 = new Sample<Vector3>(Interpolator.GetSecondVirtualSampleValue(virtual2.Time),
                        virtual2.Stopped, virtual2.Time, virtual2.Latency);
                }
                else
                {
                    newVirtual2 = ScaleSample(buffer.VirtualSamples.Value.Second, factor);
                }

                buffer.VirtualSamples = (newVirtual1, newVirtual2);
            }

            if (Interpolator.HasLastInterpolatedValue)
            {
                Interpolator.LastInterpolatedValue = ScaleValue(Interpolator.LastInterpolatedValue, factor);
            }

            Interpolator.Smoothing.CurrentVelocity = Vector3.Scale(Interpolator.Smoothing.CurrentVelocity, factor);
        }

        public override void OnConnectedEntityChanged()
        {
            MarkAsReadyToSend();
        }

        internal override (bool, string) IsBindingValid()
        {
            bool isValid = unityComponent.transform.parent == null;
            string reason = string.Empty;

            if (!isValid)
            {
                reason = "World scale binding shouldn't be in a child object.";
            }

            return (isValid, reason);
        }
    }
}
