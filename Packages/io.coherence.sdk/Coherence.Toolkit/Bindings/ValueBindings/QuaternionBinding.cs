namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using System;
    using Coherence.Toolkit.Debugging;
    using UnityEngine;

    [Serializable]
    public class QuaternionBinding : ValueBinding<Quaternion>
    {
        protected QuaternionBinding() { }
        public QuaternionBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override Quaternion Value
        {
            get => (Quaternion)GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override bool DiffersFrom(Quaternion first, Quaternion second)
        {
            return first.x != second.x
                   || first.y != second.y
                   || first.z != second.z
                   || first.w != second.w;
        }

        protected override Quaternion GetCompressedValue(Quaternion value)
        {
            var baseLod = archetypeData.GetLODstep(0);
            return Brook.Utils.CompressQuaternion(value.ToCoreQuaternion(), baseLod.Bits).ToUnityQuaternion();
        }

        [InterpolationDebugContextItem("quaternion X")]
        public void ToggleInterpolationDebugGrapherForQuaternionX() => ToggleInterpolationDebugGrapher(q => q.x, "quaternion X");

        [InterpolationDebugContextItem("quaternion Y")]
        public void ToggleInterpolationDebugGrapherForQuaternionY() => ToggleInterpolationDebugGrapher(q => q.y, "quaternion Y");

        [InterpolationDebugContextItem("quaternion Z")]
        public void ToggleInterpolationDebugGrapherForQuaternionZ() => ToggleInterpolationDebugGrapher(q => q.z, "quaternion Z");

        [InterpolationDebugContextItem("quaternion W")]
        public void ToggleInterpolationDebugGrapherForQuaternionW() => ToggleInterpolationDebugGrapher(q => q.w, "quaternion W");

        [InterpolationDebugContextItem("euler X")]
        public void ToggleInterpolationDebugGrapherForEulerX() => ToggleInterpolationDebugGrapher(q => q.eulerAngles.x, "euler X");

        [InterpolationDebugContextItem("euler Y")]
        public void ToggleInterpolationDebugGrapherForEulerY() => ToggleInterpolationDebugGrapher(q => q.eulerAngles.y, "euler Y");

        [InterpolationDebugContextItem("euler Z")]
        public void ToggleInterpolationDebugGrapherForEulerZ() => ToggleInterpolationDebugGrapher(q => q.eulerAngles.z, "euler Z");

        [InterpolationDebugContextItem("angle from identity")]
        public void ToggleInterpolationDebugGrapherForAngleFromIdentity() =>
            ToggleInterpolationDebugGrapher(q => Quaternion.Angle(Quaternion.identity, q), "angle from identity");
    }
}
