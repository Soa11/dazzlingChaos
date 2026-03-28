// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;
    using Runtime;

    /// <summary>
    /// Edit mode unit test for <see cref="Updater"/>.
    /// </summary>
    public class UpdaterTests : CoherenceTest
    {
        [Test]
        public void UpdateAll_Updates_All_Update_Listeners_Once()
        {
            var updatable1 = new Updatable();
            var updatable2 = new Updatable();
            var lateUpdatable = new LateUpdatable();
            Updater.RegisterForUpdate(updatable1);
            Updater.RegisterForUpdate(updatable2);
            Updater.RegisterForLateUpdate(lateUpdatable);
            Assert.That(updatable1.UpdateCalledTimes, Is.Zero);
            Assert.That(updatable2.UpdateCalledTimes, Is.Zero);
            Updater.UpdateAll();
            Assert.That(updatable1.UpdateCalledTimes, Is.EqualTo(1));
            Assert.That(updatable2.UpdateCalledTimes, Is.EqualTo(1));
            Assert.That(lateUpdatable.LateUpdateCalledTimes, Is.Zero);
        }

        [Test]
        public void LateUpdateAll_Updates_All_LateUpdate_Listeners_Once()
        {
            var lateUpdatable1 = new LateUpdatable();
            var lateUpdatable2 = new LateUpdatable();
            var updatable = new Updatable();
            Updater.RegisterForLateUpdate(lateUpdatable1);
            Updater.RegisterForLateUpdate(lateUpdatable2);
            Updater.RegisterForUpdate(updatable);
            Assert.That(lateUpdatable1.LateUpdateCalledTimes, Is.Zero);
            Assert.That(lateUpdatable2.LateUpdateCalledTimes, Is.Zero);
            Updater.LateUpdateAll();
            Assert.That(lateUpdatable1.LateUpdateCalledTimes, Is.EqualTo(1));
            Assert.That(lateUpdatable2.LateUpdateCalledTimes, Is.EqualTo(1));
            Assert.That(updatable.UpdateCalledTimes, Is.Zero);
        }

        [Test]
        public void UpdateAll_Executes_On_Main_Thread_Actions_Once()
        {
            var actionExecutedTimes = 0;
            Updater.ExecuteOnMainThread(() => actionExecutedTimes++);
            Assert.That(actionExecutedTimes, Is.Zero);
            Updater.UpdateAll();
            Updater.UpdateAll();
            Updater.UpdateAll();
            Assert.That(actionExecutedTimes, Is.EqualTo(1));
        }

        [Test]
        public void UpdateAll_Supports_Items_Being_Modified_During_Iteration()
        {
            var a = new DeregisterSelfDuringUpdate();
            var c = new Updatable();
            var b = new DeregisterDuringUpdate(c);
            var e = new Updatable();
            var d = new RegisterDuringUpdate(e);
            try
            {
                // We expect LateUpdateAll to update items in reverse order from which they were registered.
                Updater.RegisterForUpdate(d);
                Updater.RegisterForUpdate(c);
                Updater.RegisterForUpdate(b);
                Updater.RegisterForUpdate(a);

                Updater.UpdateAll();

                Assert.That(a.UpdateCalledTimes, Is.EqualTo(1));
                Assert.That(b.UpdateCalledTimes, Is.EqualTo(1));
                Assert.That(c.UpdateCalledTimes, Is.Zero);
                Assert.That(d.UpdateCalledTimes, Is.EqualTo(1));
                Assert.That(e.UpdateCalledTimes, Is.Zero);

                Updater.UpdateAll();

                Assert.That(a.UpdateCalledTimes, Is.EqualTo(1));
                Assert.That(b.UpdateCalledTimes, Is.EqualTo(2));
                Assert.That(c.UpdateCalledTimes, Is.Zero);
                Assert.That(d.UpdateCalledTimes, Is.EqualTo(2));
                Assert.That(e.UpdateCalledTimes, Is.EqualTo(1));
            }
            finally
            {
                Updater.DeregisterForUpdate(a);
                Updater.DeregisterForUpdate(b);
                Updater.DeregisterForUpdate(c);
                Updater.DeregisterForUpdate(d);
                Updater.DeregisterForUpdate(e);
                Updater.DeregisterForUpdate(e);
            }
        }

        [Test]
        public void LateUpdateAll_Supports_Items_Being_Modified_During_Iteration()
        {
            var a = new DeregisterSelfDuringLateUpdate();
            var c = new LateUpdatable();
            var b = new DeregisterDuringLateUpdate(c);
            var e = new LateUpdatable();
            var d = new RegisterDuringLateUpdate(e);
            try
            {
                // We expect LateUpdateAll to update items in reverse order from which they were registered.
                Updater.RegisterForLateUpdate(d);
                Updater.RegisterForLateUpdate(c);
                Updater.RegisterForLateUpdate(b);
                Updater.RegisterForLateUpdate(a);

                Updater.LateUpdateAll();

                Assert.That(a.LateUpdateCalledTimes, Is.EqualTo(1));
                Assert.That(b.LateUpdateCalledTimes, Is.EqualTo(1));
                Assert.That(c.LateUpdateCalledTimes, Is.Zero);
                Assert.That(d.LateUpdateCalledTimes, Is.EqualTo(1));
                Assert.That(e.LateUpdateCalledTimes, Is.Zero);

                Updater.LateUpdateAll();

                Assert.That(a.LateUpdateCalledTimes, Is.EqualTo(1));
                Assert.That(b.LateUpdateCalledTimes, Is.EqualTo(2));
                Assert.That(c.LateUpdateCalledTimes, Is.Zero);
                Assert.That(d.LateUpdateCalledTimes, Is.EqualTo(2));
                Assert.That(e.LateUpdateCalledTimes, Is.EqualTo(1));
            }
            finally
            {
                Updater.DeregisterForLateUpdate(a);
                Updater.DeregisterForLateUpdate(b);
                Updater.DeregisterForLateUpdate(c);
                Updater.DeregisterForLateUpdate(d);
                Updater.DeregisterForLateUpdate(e);
                Updater.DeregisterForLateUpdate(e);
            }
        }

        private sealed class Updatable : IUpdatable
        {
            public int UpdateCalledTimes { get; private set; }
            public void Update() => UpdateCalledTimes++;
        }

        private sealed class LateUpdatable : ILateUpdatable
        {
            public int LateUpdateCalledTimes { get; private set; }
            public void LateUpdate() => LateUpdateCalledTimes++;
        }

        private sealed class DeregisterSelfDuringUpdate : IUpdatable
        {
            public int UpdateCalledTimes { get; private set; }

            public void Update()
            {
                UpdateCalledTimes++;
                Updater.DeregisterForUpdate(this);
            }
        }

        private sealed class DeregisterDuringUpdate : IUpdatable
        {
            public int UpdateCalledTimes { get; private set; }
            private readonly IUpdatable[] updatablesToDeregister;

            public DeregisterDuringUpdate(params IUpdatable[] updatablesToDeregister) => this.updatablesToDeregister = updatablesToDeregister;

            public void Update()
            {
                UpdateCalledTimes++;
                foreach (var deregister in updatablesToDeregister)
                {
                    Updater.DeregisterForUpdate(deregister);
                }
            }
        }

        private sealed class RegisterDuringUpdate : IUpdatable
        {
            public int UpdateCalledTimes { get; private set; }
            private readonly IUpdatable[] updatablesToRegister;

            public RegisterDuringUpdate(params IUpdatable[] updatablesToRegister) => this.updatablesToRegister = updatablesToRegister;

            public void Update()
            {
                UpdateCalledTimes++;
                foreach (var register in updatablesToRegister)
                {
                    Updater.RegisterForUpdate(register);
                }
            }
        }

        private sealed class DeregisterSelfDuringLateUpdate : ILateUpdatable
        {
            public int LateUpdateCalledTimes { get; private set; }

            public void LateUpdate()
            {
                LateUpdateCalledTimes++;
                Updater.DeregisterForLateUpdate(this);
            }
        }

        private sealed class DeregisterDuringLateUpdate : ILateUpdatable
        {
            public int LateUpdateCalledTimes { get; private set; }
            private readonly ILateUpdatable[] lateUpdatablesToDeregister;

            public DeregisterDuringLateUpdate(params ILateUpdatable[] lateUpdatablesToDeregister) => this.lateUpdatablesToDeregister = lateUpdatablesToDeregister;

            public void LateUpdate()
            {
                LateUpdateCalledTimes++;
                foreach (var deregister in lateUpdatablesToDeregister)
                {
                    Updater.DeregisterForLateUpdate(deregister);
                }
            }
        }

        private sealed class RegisterDuringLateUpdate : ILateUpdatable
        {
            public int LateUpdateCalledTimes { get; private set; }
            private readonly ILateUpdatable[] lateUpdatablesToRegister;

            public RegisterDuringLateUpdate(params ILateUpdatable[] lateUpdatablesToRegister) => this.lateUpdatablesToRegister = lateUpdatablesToRegister;

            public void LateUpdate()
            {
                LateUpdateCalledTimes++;
                foreach (var register in lateUpdatablesToRegister)
                {
                    Updater.RegisterForLateUpdate(register);
                }
            }
        }
    }
}
