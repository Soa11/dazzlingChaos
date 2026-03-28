// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities.Tests
{
    using System.Collections.Generic;
    using Coherence.SimulationFrame;
    using Coherence.Tests;
    using NUnit.Framework;

    public class SortedValueMapTests
    {
        [Description("Verifies that sorting the map does not allocate any memory.")]
        [Test]
        public void Sort_NoAllocations()
        {
            // Arrange
            var iterations = 500;

            var componentData = new ComponentMock();
            var componentChange = ComponentChange.New(componentData);

            var map = new SortedValueMap<uint, ComponentChange>(ComponentChangeComparer.Cached, capacity: iterations);

            // Warm up
            for (var i = 0; i < iterations; i++)
            {
                map.Add((uint)i, componentChange);
                var sorted = map.Sorted;
            }

            map.Clear();

            // Act
            var allocated = AllocCounter.Instrument(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    map.Add((uint)i, componentChange);
                    var sorted = map.Sorted;
                }
            });

            // Assert
            Assert.That(allocated, Is.EqualTo(0));
        }

        public struct ComponentMock : ICoherenceComponentData
        {
            public int GetComponentOrder() => 1;
            public uint GetComponentType() => 1;


            public uint FieldsMask { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
            public uint StoppedMask { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

            public ICoherenceComponentData Clone() => throw new System.NotImplementedException();
            public uint DiffWith(ICoherenceComponentData data) => throw new System.NotImplementedException();
            public HashSet<Entity> GetEntityRefs() => throw new System.NotImplementedException();
            public int GetFieldCount() => throw new System.NotImplementedException();
            public AbsoluteSimulationFrame? GetMinSimulationFrame() => throw new System.NotImplementedException();
            public long[] GetSimulationFrames() => throw new System.NotImplementedException();
            public bool HasFields() => throw new System.NotImplementedException();
            public bool HasRefFields() => throw new System.NotImplementedException();
            public uint InitialFieldsMask() => throw new System.NotImplementedException();
            public bool IsSendOrdered() => throw new System.NotImplementedException();
            public bool IsWorldPositionComponent() => throw new System.NotImplementedException();
            public IEntityMapper.Error MapToAbsolute(IEntityMapper mapper) => throw new System.NotImplementedException();
            public IEntityMapper.Error MapToRelative(IEntityMapper mapper) => throw new System.NotImplementedException();
            public ICoherenceComponentData MergeWith(ICoherenceComponentData data) => throw new System.NotImplementedException();
            public int PriorityLevel() => throw new System.NotImplementedException();
            public uint ReplaceReferences(Entity fromEntity, Entity toEntity) => throw new System.NotImplementedException();
            public void ResetFrame(AbsoluteSimulationFrame frame) => throw new System.NotImplementedException();
            public void ClearReferences(ISet<Entity> clearEntities) => throw new System.NotImplementedException();
        }
    }
}
