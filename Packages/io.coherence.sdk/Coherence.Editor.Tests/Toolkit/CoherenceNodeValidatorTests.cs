
namespace Coherence.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using Coherence.Toolkit;
    using Coherence.Tests;
    using UnityEditor;
    using static Coherence.Toolkit.CoherenceSync;
    using static Editor.Toolkit.CoherenceNodeValidator;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit tests for <see cref="Coherence.Editor.Toolkit.CoherenceNodeValidator"/>.
    /// </summary>
    public class CoherenceNodeValidatorTests : CoherenceTest
    {
        private readonly List<CoherenceSync> syncs = new();

        private CoherenceSync Sync => Sync1;
        private CoherenceSync Sync1 => syncs[0];
        private CoherenceSync Sync2 => syncs[1];

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            CreateGameObjectWithSyncAndNode();
            CreateGameObjectWithSyncAndNode();

            void CreateGameObjectWithSyncAndNode()
            {
                var gameObject = new GameObject("GameObject" + syncs.Count + 1);
                var sync = gameObject.AddComponent<CoherenceSync>();
                gameObject.AddComponent<CoherenceNode>();
                syncs.Add(sync);
            }
        }

        [Test]
        public void Validate_Detects_Invalid_Rigidbody_Mode_And_Interpolation_Loop_Values_AllCombinations()
        {
            var rigidbodyModeOptions = GetAllRigidbodyModeOptions();
            var interpolationLoopOptions = GetAllInterpolationLoopOptions();

            foreach (var sync1RigidbodyMode in rigidbodyModeOptions)
            {
                foreach (var sync1InterpolationLoop in interpolationLoopOptions)
                {
                    Sync.RigidbodyUpdateMode = sync1RigidbodyMode;
                    Sync.InterpolationLocationConfig = sync1InterpolationLoop;

                    var issues = Validate(Sync);

                    var isSyncRigidbodyModeValid = sync1RigidbodyMode is RigidbodyMode.Direct;
                    var isSyncInterpolationLoopValid = sync1InterpolationLoop is not default(InterpolationLoop);

                    if (isSyncRigidbodyModeValid)
                    {
                        if (isSyncInterpolationLoopValid)
                        {
                            Assert.That(issues, Is.EqualTo(Issue.None));
                        }
                        else
                        {
                            Assert.That(issues, Is.EqualTo(Issue.InvalidInterpolationLocation));
                        }
                    }
                    else if (isSyncInterpolationLoopValid)
                    {
                        Assert.That(issues, Is.EqualTo(Issue.InvalidRigidbodyUpdateMode));
                    }
                    else
                    {
                        Assert.That(issues, Is.EqualTo(Issue.InvalidRigidbodyUpdateMode | Issue.InvalidInterpolationLocation));
                    }
                }
            }
        }

        [Test]
        public void FixInvalidInterpolationLocation_Fixes_Invalid_Syncs()
        {
            var interpolationLoopOptions = GetAllInterpolationLoopOptions();
            foreach (var sync1Value in interpolationLoopOptions)
            {
                foreach (var sync2Value in interpolationLoopOptions)
                {
                    Sync1.InterpolationLocationConfig = sync1Value;
                    Sync2.InterpolationLocationConfig = sync2Value;

                    using var serializedObject = new SerializedObject(new Object[]
                    {
                        Sync1,
                        Sync2
                    });
                    FixInvalidInterpolationLocation(serializedObject);
                    serializedObject.ApplyModifiedProperties();

                    Assert.That(Sync1.InterpolationLocationConfig, Is.EqualTo(sync1Value is default(InterpolationLoop) ? InterpolationLoop.Update : sync1Value));
                    Assert.That(Sync2.InterpolationLocationConfig, Is.EqualTo(sync2Value is default(InterpolationLoop) ? InterpolationLoop.Update : sync2Value));
                }
            }
        }

        [Test]
        public void FixRigidBodyUpdateMode_Fixes_All_Syncs()
        {
            foreach (var sync1Value in GetAllRigidbodyModeOptions())
            {
                foreach (var sync2Value in GetAllRigidbodyModeOptions())
                {
                    Sync1.RigidbodyUpdateMode = sync1Value;
                    Sync2.RigidbodyUpdateMode = sync2Value;

                    using var serializedObject = new SerializedObject(new Object[] { Sync1, Sync2 });
                    FixRigidBodyUpdateMode(serializedObject);
                    serializedObject.ApplyModifiedProperties();

                    Assert.That(Sync1.RigidbodyUpdateMode, Is.EqualTo(RigidbodyMode.Direct));
                    Assert.That(Sync2.RigidbodyUpdateMode, Is.EqualTo(RigidbodyMode.Direct));
                }
            }
        }

        private static List<InterpolationLoop> GetAllInterpolationLoopOptions()
        {
            var interpolationLoopOptions = Enum.GetValues(typeof(InterpolationLoop)).Cast<InterpolationLoop>().ToList();

            // Add "None" flag if missing.
            if (!interpolationLoopOptions.Contains(default))
            {
                interpolationLoopOptions.Insert(0, default);
            }

            return interpolationLoopOptions;
        }

        private static RigidbodyMode[] GetAllRigidbodyModeOptions() => Enum.GetValues(typeof(RigidbodyMode)).Cast<RigidbodyMode>().ToArray();
    }
}
