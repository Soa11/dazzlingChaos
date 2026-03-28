// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using Interpolation;

    [Serializable]
    internal class BindingArchetypeData : IEquatable<BindingArchetypeData>
    {
        internal bool IsFloatType => IsFloatBased(SchemaType);
        public SchemaType SchemaType { get => type; internal set => type = value; }
        public List<BindingLODStepData> Fields => fields;
        public long MinRange => minRange;
        public long MaxRange => maxRange;
        public ulong TotalRange => (ulong)Math.Round(maxRange - (double)minRange);
        public float SampleRate => sampleRate;
        internal FloatCompression FloatCompression => floatCompression;
        [SerializeField] protected SchemaType type;
        [SerializeField] protected long minRange;
        [SerializeField] protected long maxRange;
        [SerializeField] protected float sampleRate = InterpolationSettings.DefaultSampleRate;
        [SerializeField] protected FloatCompression floatCompression;
        [SerializeField] protected List<BindingLODStepData> fields = new();

        // For Unity deserialization
        private BindingArchetypeData() { }

        public BindingArchetypeData(SchemaType type, Type valueType)
        {
            this.type = type;
            SetRangesToDefaultValues(valueType);
        }

        internal bool CanOverride(bool isMethod)
            => type is SchemaType.Int or SchemaType.UInt or SchemaType.Float or SchemaType.Vector2 or SchemaType.Vector3 or SchemaType.Color or SchemaType.Quaternion
            || isMethod;

        internal bool IsRangeType() => SchemaType is SchemaType.Int or SchemaType.UInt || (IsFloatType && FloatCompression is FloatCompression.FixedPoint);
        internal static bool IsBitsBased(SchemaType type) => type is SchemaType.Color or SchemaType.Quaternion;
        internal static bool IsFloatBased(SchemaType type) => type is SchemaType.Float or SchemaType.Vector2 or SchemaType.Vector3;

        internal void SetRange(long minRange, long maxRange)
        {
            this.minRange = minRange;
            this.maxRange = maxRange;
        }

        internal void SetSampleRate(float sampleRate) => this.sampleRate = sampleRate;
        internal void SetFloatCompression(FloatCompression floatCompression) => this.floatCompression = floatCompression;

        internal virtual void CopyFrom(BindingArchetypeData other)
        {
            type = other.type;
            minRange = other.minRange;
            maxRange = other.maxRange;
            floatCompression = other.floatCompression;

            if (fields.Count > other.fields.Count)
            {
                fields.RemoveRange(other.fields.Count, fields.Count - other.fields.Count);
            }

            for (var i = 0; i < other.fields.Count; i++)
            {
                if (fields.Count > i)
                {
                    fields[i].CopyFrom(other.fields[i], this);
                }
                else
                {
                    fields.Add(new(other.fields[i], this));
                }
            }
        }

        internal bool Update(SchemaType type, Type valueType, int lodsteps)
        {
            var changed = false;
            var isNewType = type != this.type;

            if (isNewType)
            {
                changed = true;
                this.type = type;
                SetRangesToDefaultValues(valueType);
            }

            changed |= AddLODStep(lodsteps);

            return changed;
        }

        internal void SetRangesToDefaultValues(Type valueType)
        {
            if (valueType != null && ArchetypeMath.TryGetTypeLimits(valueType, out double typeMinRange, out double typeMaxRange))
            {
                minRange = (long)Math.Round(typeMinRange);
                maxRange = (long)Math.Round(typeMaxRange);
                return;
            }

            switch (SchemaType)
            {
                case SchemaType.Int:
                    minRange = int.MinValue;
                    maxRange = int.MaxValue;
                    break;
                case SchemaType.UInt:
                    minRange = uint.MinValue;
                    maxRange = uint.MaxValue;
                    break;
                case SchemaType.Enum:
                    var (_, min, max) = ArchetypeMath.GetRangeAndBitsForEnum(valueType);

                    minRange = min;
                    maxRange = max;
                    break;
            }
        }

        public int GetTotalBitsOfLOD(int lodStep)
        {
            if (fields.Count > lodStep)
            {
                return fields[lodStep].TotalBits;
            }
            return 0;
        }

        internal BindingLODStepData GetLODstep(int lodStep)
        {
            if (fields == null || fields.Count == 0)
            {
                return null;
            }

            if (lodStep < fields.Count)
            {
                return fields[lodStep];
            }
            // This can happen on old data, if the user has done their own editing etc.
            return fields[^1];
        }

        internal bool AddLODStep(int lodStep)
        {
            bool changed = InstantiateFieldsList();

            while (fields.Count < lodStep)
            {
                changed = true;
                if (fields.Count > 0)
                {
                    BindingLODStepData newField = new BindingLODStepData(fields[fields.Count - 1], this);
                    newField.UpdateModel(this);
                    fields.Add(newField);
                }
                else
                {
                    BindingLODStepData newField = new BindingLODStepData(this);
                    fields.Add(newField);
                }
            }

            return changed;
        }

        private bool InstantiateFieldsList()
        {
            if (fields == null)
            {
                fields = new();
                return true;
            }

            return false;
        }

        internal void RemoveLODLevel(int lodStep, int maxLods)
        {
            if (fields.Count > lodStep)
            {
                fields.RemoveAt(lodStep);
            }
            while (fields.Count > maxLods)
            {
                fields.RemoveAt(fields.Count - 1);
            }
        }

        internal void ResetValuesToDefault(Type bindingValueType, bool resetRanges, bool resetBitsAndPrecision)
        {
            if (resetRanges)
            {
                ResetRanges(bindingValueType);
            }

            if (resetBitsAndPrecision)
            {
                ResetBitsAndPrecision();
            }

            foreach (var field in Fields)
            {
                field.Verify(minRange, maxRange);
            }
        }

        internal (long min, long max) GetRangeByLODs()
        {
            long min = long.MinValue;
            long max = long.MaxValue;

            foreach (BindingLODStepData lod in fields)
            {
                (long lodMin, long lodMax) = ArchetypeMath.GetRangeByBitsAndPrecision(lod.Bits, lod.Precision);
                min = Math.Max(min, lodMin);
                max = Math.Min(max, lodMax);
            }

            return (min, max);
        }

        internal void ResetRanges(Type bindingValueType)
        {
            if (!IsRangeType())
            {
                minRange = 0;
                maxRange = 0;
                return;
            }

            if (IsFloatType && floatCompression == FloatCompression.FixedPoint)
            {
                (long min, long max) = ArchetypeMath.GetRangeByBitsAndPrecision(
                    BindingLODStepData.FLOAT_DEFAULT_BITS,
                    BindingLODStepData.FLOAT_DEFAULT_PRECISION);
                this.minRange = min;
                this.maxRange = max;
            }
            else
            {
                if (ArchetypeMath.TryGetTypeLimits(bindingValueType,
                        out double typeMinRange, out double typeMaxRange))
                {
                    this.minRange = (long)typeMinRange;
                    this.maxRange = (long)typeMaxRange;
                }
                else
                {
                    Debug.LogError($"Failed to get type limits for {bindingValueType}");
                }
            }
        }

        private void ResetBitsAndPrecision()
        {
            foreach (var field in Fields)
            {
                field.SetDefaultOverrides(SchemaType);
                field.Verify(minRange, maxRange);
            }
        }

        internal bool FixSerializedDataInFields()
        {
            if (InstantiateFieldsList())
            {
                return true;
            }

            bool changed = false;
            int index = 0;
            foreach (var field in fields)
            {
                if (field.SchemaType != SchemaType.Unknown && field.Bits == 0)
                {
                    changed = true;
                    field.UpdateModel(this, true);
                }

                index++;
            }

            return changed;
        }

        public bool Equals(BindingArchetypeData other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(type, other.type)
                && minRange.Equals(other.minRange)
                && maxRange.Equals(other.maxRange)
                && sampleRate.Equals(other.sampleRate)
                && floatCompression == other.floatCompression
                && (fields?.SequenceEqual(other.fields) ?? other.fields is null);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((BindingArchetypeData)obj);
        }

        public override int GetHashCode() => HashCode.Combine((int)type, minRange, maxRange, sampleRate, (int)floatCompression, fields);

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties found in <see cref="BindingArchetypeData"/>.
        /// Can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string type = nameof(BindingArchetypeData.type);
            public const string minRange = nameof(BindingArchetypeData.minRange);
            public const string maxRange = nameof(BindingArchetypeData.maxRange);
            public const string sampleRate = nameof(BindingArchetypeData.sampleRate);
            public const string floatCompression = nameof(BindingArchetypeData.floatCompression);
            public const string fields = nameof(BindingArchetypeData.fields);
        }
#endif
    }
}
