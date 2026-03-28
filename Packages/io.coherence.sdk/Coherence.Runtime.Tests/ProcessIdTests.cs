// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Prefs.Tests
{
    using System;
    using System.Threading.Tasks;
    using Coherence.Tests;
    using NUnit.Framework;
    using Runtime.Utils;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit test for <see cref="ProcessId"/>.
    /// </summary>
    public class ProcessIdTests : CoherenceTest
    {
        private const string ProjectId = "TestProjectId";

        [Test]
        public void Claiming_Works()
        {
            var prefs = new FakePrefs();
            var claimedId = ProcessId.FirstProcessId;
            var claimForSeconds = 10d;
            var claimUntil = DateTime.UtcNow + TimeSpan.FromSeconds(claimForSeconds);
            var prefsKey = ProcessId.GetPrefsKey(ProjectId, claimedId);

            ProcessId.ClaimUntil(prefs, prefsKey, claimUntil);
            Assert.That(ProcessId.GetClaimedUntilTime(prefs, ProjectId, claimedId), Is.EqualTo(claimUntil));
            Assert.That(ProcessId.IsClaimed(prefs, ProjectId, claimedId), Is.True);

            var unclaimedId = ProcessId.GetFirstUnclaimedId(prefs, ProjectId);
            Assert.That(unclaimedId, Is.EqualTo(claimedId + 1));
            Assert.That(ProcessId.IsClaimed(prefs, ProjectId, unclaimedId), Is.False);

            ProcessId.Release(prefs, prefsKey);
            Assert.That(ProcessId.IsClaimed(prefs, ProjectId, claimedId), Is.False);
        }

        [Test]
        public void Get_Always_Returns_FirstProcessId_In_Edit_Mode()
            => Assert.That(ProcessId.Get(), Is.EqualTo(ProcessId.FirstProcessId));

        [Test]
        public async Task Get_Does_Not_Create_ProcessId_Instance_In_Edit_Mode()
        {
            _ = ProcessId.Get();

            // Wait a few frames, because ProcessId can defer the creation
            // of the instance to ensure it happens on the main thread.
            await Wait.ForNextFrame();
            await Wait.ForNextFrame();

            Assert.That((bool)Object.FindAnyObjectByType<ProcessId>(FindObjectsInactive.Include), Is.False);
        }
    }
}
