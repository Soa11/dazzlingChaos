// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tests
{
    using System;
    using UnityEngine.Profiling;

    // Borrowed directly from UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint
    public class AllocCounter
    {
        private Recorder _rec;

        public AllocCounter()
        {
            _rec = Recorder.Get("GC.Alloc");

            // The recorder was created enabled, which means it captured the creation of the
            // Recorder object itself, etc. Disabling it flushes its data, so that we can retrieve
            // the sample block count and have it correctly account for these initial allocations.
            _rec.enabled = false;

#if !UNITY_WEBGL
            _rec.FilterToCurrentThread();
#endif

            _rec.enabled = true;
        }

        public int Stop()
        {
            if (_rec == null)
            {
                throw new Exception("AllocCounter already stopped");
            }

            _rec.enabled = false;

#if !UNITY_WEBGL
            _rec.CollectFromAllThreads();
#endif

            var res = _rec.sampleBlockCount;
            _rec = null;
            return res;
        }

        public static int Instrument(Action action)
        {
            var counter = new AllocCounter();
            int allocs;
            try
            {
                action();
            }
            finally
            {
                allocs = counter.Stop();
            }

            return allocs;
        }
    }
}
