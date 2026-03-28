namespace Coherence.Toolkit.Tests
{
    using Bindings;
    using Bindings.ValueBindings;
    using NUnit.Framework;
    using UnityEngine;
    using Coherence.Tests;
    using Coherence.Common.Tests;
    using Coherence.Log;
    using Interpolation;

    public class ValueBindingTests : CoherenceTest
    {
        public class TestBehaviour : MonoBehaviour
        {
            public int testInt;
        }

        private TestBehaviour testBehaviour;
        private IntBinding binding;
        private InterpolationSettings interpolationSettings;

        public override void OneTimeSetUp() => interpolationSettings = InterpolationSettings.CreateDefault();
        public override void OneTimeTearDown() => Object.DestroyImmediate(interpolationSettings);

        public override void SetUp()
        {
            base.SetUp();

            testBehaviour = new GameObject().AddComponent<TestBehaviour>();

            var memberInfo = typeof(TestBehaviour).GetMember("testInt")[0];
            var descriptor = new Descriptor(typeof(TestBehaviour), memberInfo);

            binding = (IntBinding)descriptor.InstantiateBinding(testBehaviour);
        }

        [Test]
        [Description("Verifies that MarkForSyncing() marks a Manual sync mode binding as dirty for network synchronization. " +
                     "The binding should be dirty on first check (initial sync), clean on second check, then dirty again after marking.")]
        public void MarkForSyncing_ManualSyncMode_MarksBindingAsDirty()
        {
            // Arrange
            binding.SyncMode = SyncMode.Manual;
            binding.Value = 42;

            // First check - should be dirty initially (forceSync = true by default for first sync)
            binding.IsDirty(0, out var isDirtyInitial, out var justStoppedInitial);
            Assert.IsTrue(isDirtyInitial, "Binding should be dirty on first check for initial sync");
            Assert.IsTrue(justStoppedInitial, "justStopped should be true on first check");

            // Second check - should NOT be dirty now (forceSync was reset)
            binding.IsDirty(0, out var isDirtySecond, out var justStoppedSecond);
            Assert.IsFalse(isDirtySecond, "Binding should NOT be dirty on second check");
            Assert.IsFalse(justStoppedSecond, "justStopped should be false on second check");

            // Act - mark for syncing
            binding.MarkForSyncing();
            binding.IsDirty(0, out var isDirtyAfterMark, out var justStoppedAfterMark);

            // Assert - should be dirty after marking
            Assert.IsTrue(isDirtyAfterMark, "Binding should be dirty after MarkForSyncing()");
            Assert.IsTrue(justStoppedAfterMark, "justStopped should be true after MarkForSyncing()");
        }

        [Test]
        [Description("Verifies that changing SyncMode at runtime can be done successfully. " +
                     "This ensures the SyncMode property is settable and supports runtime mode transitions " +
                     "between Manual and Always sync modes.")]
        public void SyncMode_RuntimeChange_SuccessfullyChangesMode()
        {
            // Arrange - set up binding with Manual sync mode
            binding.SyncMode = SyncMode.Manual;
            Assert.AreEqual(SyncMode.Manual, binding.SyncMode, "SyncMode should be Manual");

            // Act - change sync mode from Manual to Always
            binding.SyncMode = SyncMode.Always;

            // Assert - sync mode should be changed
            Assert.AreEqual(SyncMode.Always, binding.SyncMode, "SyncMode should be changed to Always");

            // Act - change sync mode back to Manual
            binding.SyncMode = SyncMode.Manual;

            // Assert - sync mode should be changed back
            Assert.AreEqual(SyncMode.Manual, binding.SyncMode, "SyncMode should be changed back to Manual");
        }

        [Test]
        [Description("Verifies that changing SyncMode at runtime resets the interpolator to prevent stale samples " +
                     "from the previous sync mode from affecting the new mode. This ensures clean state transitions " +
                     "when switching between Manual and Always sync modes.")]
        public void SyncMode_RuntimeChange_ResetsInterpolator()
        {
            // Arrange - set up binding with Manual sync mode
            binding.SyncMode = SyncMode.Manual;
            binding.Value = 10;

            // Activate the binding to initialize the interpolator
            binding.CreateArchetypeData(SchemaType.Int, 0);
            binding.Activate();
            Assert.IsNotNull(binding.Interpolator, "Interpolator should be initialized after Activate()");

            // Add a sample to the interpolator
            binding.Interpolator.AppendSample(10, false, true, 0.0, 0.0);
            var sampleCountBefore = binding.Interpolator.Buffer.Count;
            Assert.AreEqual(1, sampleCountBefore, "Should have one sample in the buffer");

            // Act - change sync mode from Manual to Always
            binding.SyncMode = SyncMode.Always;

            // Assert - interpolator should be reset (buffer cleared)
            var sampleCountAfter = binding.Interpolator.Buffer.Count;
            Assert.AreEqual(0, sampleCountAfter, "Interpolator buffer should be cleared after SyncMode change");
        }

        [Test]
        [Description("Verifies that in Always sync mode, IsDirty() automatically detects value changes without requiring MarkForSyncing(). " +
                     "This is the default behavior where the binding monitors for changes and syncs them automatically.")]
        public void IsDirty_AlwaysMode_DetectsValueChange()
        {
            // Arrange
            binding.SyncMode = SyncMode.Always;
            binding.Value = 10;

            // First check - should be dirty due to forceSync = true initially
            binding.IsDirty(0, out var isDirtyInitial, out _);
            Assert.IsTrue(isDirtyInitial, "Binding should be dirty on first check (forceSync)");

            // Second check - should NOT be dirty (no change)
            binding.IsDirty(0, out var isDirtyNoChange, out _);
            Assert.IsFalse(isDirtyNoChange, "Binding should NOT be dirty when value hasn't changed");

            // Act - change the value
            binding.Value = 20;

            // Assert - should be dirty after value change
            binding.IsDirty(0, out var isDirtyAfterChange, out _);
            Assert.IsTrue(isDirtyAfterChange, "Binding should be dirty after value change in Always mode");
        }

        [Test]
        [Description("Verifies that in Manual sync mode, IsDirty() ignores automatic value changes and only reports dirty " +
                     "when MarkForSyncing() is explicitly called. This provides fine-grained control over network traffic.")]
        public void IsDirty_ManualMode_IgnoresValueChanges()
        {
            // Arrange
            binding.SyncMode = SyncMode.Manual;
            binding.Value = 10;

            // First check - should be dirty due to forceSync = true initially
            binding.IsDirty(0, out var isDirtyInitial, out _);
            Assert.IsTrue(isDirtyInitial, "Binding should be dirty on first check (forceSync)");

            // Act - change the value without calling MarkForSyncing()
            binding.Value = 20;

            // Assert - should NOT be dirty (Manual mode ignores automatic changes)
            binding.IsDirty(0, out var isDirtyAfterChange, out _);
            Assert.IsFalse(isDirtyAfterChange, "Binding should NOT be dirty in Manual mode without MarkForSyncing()");

            // Act - explicitly mark for syncing
            binding.MarkForSyncing();

            // Assert - should be dirty after marking
            binding.IsDirty(0, out var isDirtyAfterMark, out _);
            Assert.IsTrue(isDirtyAfterMark, "Binding should be dirty after MarkForSyncing() in Manual mode");
        }

        [Test]
        [Description("Verifies that MarkForSyncing() can be called multiple times consecutively and each call " +
                     "marks the binding as dirty for the next IsDirty() check, then resets until the next MarkForSyncing() call.")]
        public void MarkForSyncing_MultipleCalls_EachMarksDirty()
        {
            // Arrange
            binding.SyncMode = SyncMode.Manual;
            binding.Value = 10;

            // Clear initial forceSync
            binding.IsDirty(0, out _, out _);

            // Act & Assert - First mark
            binding.MarkForSyncing();
            binding.IsDirty(0, out var isDirtyFirst, out _);
            Assert.IsTrue(isDirtyFirst, "Binding should be dirty after first MarkForSyncing()");

            // Should NOT be dirty on second check
            binding.IsDirty(0, out var isDirtySecond, out _);
            Assert.IsFalse(isDirtySecond, "Binding should NOT be dirty on second check without MarkForSyncing()");

            // Act & Assert - Second mark
            binding.MarkForSyncing();
            binding.IsDirty(0, out var isDirtyThird, out _);
            Assert.IsTrue(isDirtyThird, "Binding should be dirty after second MarkForSyncing()");

            // Should NOT be dirty again
            binding.IsDirty(0, out var isDirtyFourth, out _);
            Assert.IsFalse(isDirtyFourth, "Binding should NOT be dirty on fourth check without MarkForSyncing()");
        }

        [Test]
        [Description("Verifies that calling MarkForSyncing() on a binding with Always sync mode logs a warning " +
                     "since MarkForSyncing() is only intended for Manual sync mode where explicit control is needed.")]
        public void MarkForSyncing_AlwaysMode_LogsWarning()
        {
            // Arrange
            var testLogger = new TestLogger();
            binding.Logger = testLogger;
            binding.SyncMode = SyncMode.Always;
            binding.Value = 10;

            // Clear initial forceSync
            binding.IsDirty(0, out _, out _);

            // Act
            binding.MarkForSyncing();

            // Assert - should log the warning
            var warningCount = testLogger.GetCountForWarningID(Warning.ToolkitBindingMarkAsDirtyNonManual);
            Assert.AreEqual(1u, warningCount, "Should log warning when calling MarkForSyncing() on Always mode");

            // Assert - should still mark as dirty despite the warning
            binding.IsDirty(0, out var isDirty, out _);
            Assert.IsTrue(isDirty, "Binding should still be marked dirty even when called on Always mode");
        }

        [Test]
        [Description("Verifies that IsReadyToSample() returns true immediately for Manual sync mode when forceSync is set. " +
                     "In Manual mode, sampling is controlled explicitly rather than by time-based intervals.")]
        public void IsReadyToSample_ManualMode_ReturnsTrueWhenForceSync()
        {
            // Arrange
            binding.SyncMode = SyncMode.Manual;
            binding.CreateArchetypeData(SchemaType.Int, 0);
            binding.Activate();

            // Initially forceSync is true
            var readyInitial = binding.IsReadyToSample(0.0);
            Assert.IsTrue(readyInitial, "Should be ready to sample initially when forceSync is true");

            // After IsDirty check, forceSync is cleared
            binding.IsDirty(0, out _, out _);
            var readyAfterDirtyCheck = binding.IsReadyToSample(0.0);
            Assert.IsFalse(readyAfterDirtyCheck, "Should NOT be ready to sample after forceSync is cleared");

            // Mark for syncing again
            binding.MarkForSyncing();
            var readyAfterMark = binding.IsReadyToSample(0.0);
            Assert.IsTrue(readyAfterMark, "Should be ready to sample after MarkForSyncing()");
        }

        [Test]
        [Description("Verifies that IsReadyToSample() respects the sample rate timing in Always mode. " +
                     "The method should only return true when enough time has passed based on the configured sample rate.")]
        public void IsReadyToSample_AlwaysMode_RespectsTimingAndSampleRate()
        {
            // Arrange
            binding.SyncMode = SyncMode.Always;
            binding.CreateArchetypeData(SchemaType.Int, 0);
            binding.Activate();

            var sampleRate = binding.Interpolator.SampleRate;
            var period = 1.0 / sampleRate;

            // Clear initial forceSync
            binding.IsDirty(0, out _, out _);

            // Act & Assert - should be ready at time 0
            var readyAtZero = binding.IsReadyToSample(0.0);
            Assert.IsTrue(readyAtZero, "Should be ready to sample at time 0");

            // Should NOT be ready immediately after
            var readyImmediatelyAfter = binding.IsReadyToSample(0.0);
            Assert.IsFalse(readyImmediatelyAfter, "Should NOT be ready immediately after sampling");

            // Should NOT be ready before period elapses
            var readyBeforePeriod = binding.IsReadyToSample(period * 0.5);
            Assert.IsFalse(readyBeforePeriod, "Should NOT be ready before period elapses");

            // Should be ready after period elapses
            var readyAfterPeriod = binding.IsReadyToSample(period);
            Assert.IsTrue(readyAfterPeriod, "Should be ready after period elapses");
        }

        [Test]
        [Description("Verifies that SampleValue() adds a sample to the interpolator buffer with the current value. " +
                     "This is used to capture snapshots of the value over time for interpolation.")]
        public void SampleValue_AlwaysMode_AddsSampleToInterpolator()
        {
            // Arrange
            binding.SyncMode = SyncMode.Always;
            binding.interpolationSettings = interpolationSettings;
            binding.CreateArchetypeData(SchemaType.Int, 0);
            binding.Activate();
            binding.Value = 42;

            var bufferCountBefore = binding.Interpolator.Buffer.Count;

            // Act
            binding.SampleValue(1.0);

            // Assert
            var bufferCountAfter = binding.Interpolator.Buffer.Count;
            Assert.AreEqual(bufferCountBefore + 1, bufferCountAfter, "Buffer should have one more sample after SampleValue()");

            var lastSample = binding.Interpolator.GetLastSample();
            Assert.IsTrue(lastSample.HasValue, "Should have a last sample");
            Assert.AreEqual(42, lastSample.Value.Value, "Last sample should contain the correct value");
        }

        [Test]
        [Description("Verifies that SampleValue() does not add samples in Manual sync mode. " +
                     "Manual mode requires explicit control via MarkForSyncing() rather than automatic sampling.")]
        public void SampleValue_ManualMode_DoesNotAddSample()
        {
            // Arrange
            binding.SyncMode = SyncMode.Manual;
            binding.interpolationSettings = interpolationSettings;
            binding.CreateArchetypeData(SchemaType.Int, 0);
            binding.Activate();
            binding.Value = 42;

            var bufferCountBefore = binding.Interpolator.Buffer.Count;

            // Act
            binding.SampleValue(1.0);

            // Assert
            var bufferCountAfter = binding.Interpolator.Buffer.Count;
            Assert.AreEqual(bufferCountBefore, bufferCountAfter, "Buffer should NOT have new samples in Manual mode");
        }

        [Test]
        [Description("Verifies that multiple SampleValue() calls at different times create multiple samples in the buffer. " +
                     "This ensures the interpolation system has enough data points for smooth interpolation.")]
        public void SampleValue_MultipleCalls_CreatesMultipleSamples()
        {
            // Arrange
            binding.SyncMode = SyncMode.Always;
            binding.interpolationSettings = interpolationSettings;
            binding.CreateArchetypeData(SchemaType.Int, 0);
            binding.Activate();

            var initialBufferCount = binding.Interpolator.Buffer.Count;

            // Act - sample multiple times with different values
            binding.Value = 10;
            binding.SampleValue(0.0);

            binding.Value = 20;
            binding.SampleValue(0.1);

            binding.Value = 30;
            binding.SampleValue(0.2);

            // Assert
            var finalBufferCount = binding.Interpolator.Buffer.Count;
            Assert.AreEqual(initialBufferCount + 3, finalBufferCount, "Should have three new samples in the buffer");
        }
    }
}
