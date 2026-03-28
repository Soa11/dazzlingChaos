// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities
{
    using System;
    using System.Collections.Generic;

    public struct ComponentChange
    {
        // this is the type used for serializing the data.
        // the Data component is a concrete type but with archetypes
        // the serialization type can be overriden at different LODs.
        public uint ComponentSerializeType { get; private set; }
        public ICoherenceComponentData Data { get; private set; }

        public static ComponentChange New(ICoherenceComponentData data)
        {
            return new ComponentChange
            {
                ComponentSerializeType = data.GetComponentType(),
                Data = data
            };
        }

        public void SetSerializeType(uint compType)
        {
            ComponentSerializeType = compType;
        }

        public ComponentChange Update(ComponentChange change)
        {
            change.Data = Data.MergeWith(change.Data);
            return change;
        }

        public ComponentChange ClearMask(uint mask)
        {
            Data.FieldsMask &= ~mask;
            return this;
        }

        public ComponentChange ClearStoppedMask(uint mask)
        {
            Data.StoppedMask &= ~mask;
            return this;
        }

        public ComponentChange Clone()
        {
            return new ComponentChange
            {
                ComponentSerializeType = ComponentSerializeType,
                Data = Data.Clone()
            };
        }

        public override string ToString()
        {
            var maskBits = Data.InitialFieldsMask() != 0
                ? Convert.ToString(Data.FieldsMask, 2).PadLeft((int)Math.Log(Data.InitialFieldsMask(), 2d), '0')
                : "0";

            return $"(T: {ComponentSerializeType} M: {maskBits} D: {Data})";
        }
    }

    internal class ComponentChangeComparer
    {
        internal static readonly Comparison<KeyValuePair<uint, ComponentChange>> Cached = (x, y) =>
        {
            var order = x.Value.Data.GetComponentOrder().CompareTo(y.Value.Data.GetComponentOrder());
            return order != 0 ? order : x.Value.Data.GetComponentType().CompareTo(y.Value.Data.GetComponentType());
        };
    }
}
