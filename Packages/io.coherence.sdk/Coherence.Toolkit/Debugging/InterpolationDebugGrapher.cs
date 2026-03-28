// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Debugging
{
    using System;
    using Coherence.Interpolation;
    using Coherence.Toolkit.Bindings;
    using UnityEngine;

    public class InterpolationDebugGrapher : MonoBehaviour
    {
        private static InterpolationDebugGrapher instance;
        public static InterpolationDebugGrapher Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<InterpolationDebugGrapher>();
                }

                if (instance == null && Application.isPlaying)
                {
                    var go = new GameObject("CoherenceInterpolationDebugGrapher");
                    instance = go.AddComponent<InterpolationDebugGrapher>();
                }

                return instance;
            }
        }

        public int InitialGraphGroup = 0;

        private DebugGUI debugGUI;

        private Binding binding;
        private Func<InterpolationResult<float>> interpolationResultGetter;
        private Func<float> currentValueGetter;
        private string extraName;

        private float? lastValue = null;

        public Binding Binding => binding;
        public string ExtraName => extraName;

        private void Awake()
        {
            var debugGUI = GetComponent<DebugGUI>();
            if (debugGUI == null)
            {
                debugGUI = gameObject.AddComponent<DebugGUI>();
            }

            this.debugGUI = debugGUI;
        }

        private void Update()
        {
            if (ShouldGraph(CoherenceSync.InterpolationLoop.Update))
            {
                Graph();
            }
        }

        private void FixedUpdate()
        {
            if (ShouldGraph(CoherenceSync.InterpolationLoop.FixedUpdate))
            {
                Graph();
            }
        }

        private void LateUpdate()
        {
            if (ShouldGraph(CoherenceSync.InterpolationLoop.LateUpdate))
            {
                Graph();
            }
        }

        private bool ShouldGraph(CoherenceSync.InterpolationLoop loop)
        {
            if (binding == null)
            {
                return false;
            }

            var sync = binding.coherenceSync;
            if (!sync)
            {
                this.Disable();
                return false;
            }

            if (!sync.InterpolationLocationConfig.HasFlag(loop))
            {
                return false;
            }

            var bridge = sync.CoherenceBridge;
            if (!bridge)
            {
                return false;
            }

            if (!bridge.IsConnected)
            {
                return false;
            }

            return true;
        }

        public void SetGraphsOnRight(bool graphOnRight)
        {
            debugGUI.IsOnRight = graphOnRight;
        }

        public void Clear()
        {
            this.debugGUI.InstanceClearPersistent();
            this.debugGUI.InstanceClearAllGraphs();
            this.binding = null;
            this.interpolationResultGetter = null;
            this.currentValueGetter = null;
            this.extraName = null;
            this.lastValue = null;
        }

        public void Disable()
        {
            Clear();
            this.debugGUI.InstanceRemoveAllGraphs();
        }

        public void SetGraphingInterpolator<T>(
            Binding binding,
            BindingInterpolator<T> interpolator,
            Func<float> valueGetter,
            Func<T, float> valueMapper,
            string extraName)
        {
            this.Clear();

            this.binding = binding;
            this.extraName = extraName;
            this.currentValueGetter = valueGetter;
            this.interpolationResultGetter = () =>
            {
                var resultT = interpolator.CalculateInterpolationPercentage(interpolator.Time, ignoreVirtualSamples: false);

                return new InterpolationResult<float>()
                {
                    sample0 = new Sample<float>(valueMapper(resultT.sample0.Value), resultT.sample0.Stopped, resultT.sample0.Time, resultT.sample0.Latency),
                    sample1 = new Sample<float>(valueMapper(resultT.sample1.Value), resultT.sample1.Stopped, resultT.sample1.Time, resultT.sample1.Latency),
                    sample2 = new Sample<float>(valueMapper(resultT.sample2.Value), resultT.sample2.Stopped, resultT.sample2.Time, resultT.sample2.Latency),
                    sample3 = new Sample<float>(valueMapper(resultT.sample3.Value), resultT.sample3.Stopped, resultT.sample3.Time, resultT.sample3.Latency),
                    t = resultT.t,
                    delay = resultT.delay,
                    targetDelay = resultT.targetDelay,
                    networkLatency = resultT.networkLatency,
                    lastSampleLatency = resultT.lastSampleLatency,
                    lastSampleInterval = resultT.lastSampleInterval,
                    measuredSampleInterval = resultT.measuredSampleInterval,
                    isStopped = resultT.isStopped,
                    virtualOvershoot = resultT.virtualOvershoot,
                };
            };
        }

        public void Graph()
        {
            if (binding == null || !binding.coherenceSync)
            {
                this.Disable();
                return;
            }

            var group = InitialGraphGroup;

            LogInfo();
            GraphValue(ref group);
            GraphInterpolator(ref group);
        }

        private void LogInfo()
        {
            var objectName = binding.coherenceSync.name;
            if (string.IsNullOrEmpty(objectName))
            {
                objectName = "<unnamed>";
            }

            var authority = binding.coherenceSync.HasStateAuthority ? "local" : "remote";
            var additionalName = string.IsNullOrEmpty(extraName) ? "" : $" ({extraName})";

            debugGUI.InstanceLogPersistent("info",
                $"Object: {objectName} ({authority}), Binding: {binding.Name}{additionalName}");
        }

        private void GraphInterpolator(ref int group)
        {
            if (interpolationResultGetter == null)
            {
                return;
            }

            var result = interpolationResultGetter.Invoke();

            // t is the percentage between sample1 and sample2
            GraphProperty("T", "t", 0, 1f, group, Color.green, true,
                value: result.t);

            var sampleDeltaTime = (float)(result.sample2.Time - result.sample1.Time);

            // sample delta time is the delta time between sample1 and sample2
            GraphProperty("SDt", "sample delta time", 0, 0.08f, group, Color.magenta, true,
                value: sampleDeltaTime);

            // is stopped indicates if the interpolation reached last, stopped, sample and is currently stopped
            GraphProperty("stopped", "is stopped", -0.5f, 1.5f, group, Color.cyan, false,
                value: result.isStopped ? 1f : 0f);

            group++;

            var sampleDeltaValue = result.sample2.Value - result.sample1.Value;
            var sampleVelocity = sampleDeltaValue / sampleDeltaTime;

            // sample velocity is the velocity between sample1 and sample2
            GraphProperty("sV", "sample velocity", -0.1f, 0.1f, group, Color.yellow, true,
                value: sampleVelocity);

            // sample1 value is the value of sample1
            GraphProperty("s1V", "sample1 value", -0.1f, 0.1f, group, Color.green, true,
                value: result.sample1.Value);

            // sample2 value is the value of sample2
            GraphProperty("s2V", "sample2 value", -0.1f, 0.1f, group, Color.cyan, true,
                value: result.sample2.Value);

            // last sample latency is the latency of the last received sample (not always sample2 nor sample3)
            GraphProperty("sL", "last sample latency", 0, 0.05f, group, Color.magenta, true,
                value: (float)result.lastSampleLatency);

            group++;

            // delay is the current delay behind client network time of this interpolator
            GraphProperty("D", "delay", 0, 0.05f, group, Color.green, true,
                value: (float)result.delay);

            // target delay is the target value that delay is trying to reach
            GraphProperty("tD", "target delay", 0, 0.05f, group, Color.yellow, true,
                value: (float)result.targetDelay);

            // network latency is the average network latency of all samples (see: SampleBuffer.TryMeasureSampleLatency())
            GraphProperty("NL", "network latency", 0, 0.05f, group, Color.magenta, true,
                value: (float)result.networkLatency);

            // measured sample interval is the average interval between samples (see: SampleBuffer.TryMeasureSampleInterval())
            GraphProperty("mSI", "measured sample interval", 0, 0.08f, group, Color.cyan, true,
                value: (float)result.measuredSampleInterval);

            // virtual overshoot is the time difference between the last sample and the second virtual sample (if any)
            GraphProperty("vo", "virtual overshoot", 0, 0.05f, group, Color.white, true,
                value: (float)result.virtualOvershoot);
        }

        private void GraphValue(ref int group)
        {
            if (currentValueGetter == null)
            {
                return;
            }

            var currentValue = currentValueGetter.Invoke();

            GraphProperty("v", "value", -0.1f, 0.1f, group, Color.green, true,
                value: currentValue);

            var velocity = 0f;
            if (lastValue != null)
            {
                velocity = (currentValue - lastValue.Value) / Time.deltaTime;
            }

            GraphProperty("mV", "measured velocity", -0.1f, 0.1f, group, Color.yellow, true,
                value: velocity);

            lastValue = currentValue;
            group++;
        }

        private void GraphProperty(string key, string label, float min, float max, int group, Color color, bool autoScale, float value)
        {
            if (!debugGUI.InstanceGetGraphExists(key))
            {
                debugGUI.InstanceSetGraphProperties(key, label, min, max, group, color, autoScale);
            }

            debugGUI.InstanceGraph(key, value);
        }
    }
}
