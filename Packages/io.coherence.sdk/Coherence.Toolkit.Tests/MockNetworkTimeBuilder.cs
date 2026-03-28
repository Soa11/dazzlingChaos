namespace Coherence.Toolkit.Tests
{
    using System;
    using Moq;
    using SimulationFrame;

    /// <summary>
    /// Can be used to <see cref="Build"/> a mock <see cref="INetworkTime"/> object that can be used as a test double.
    /// </summary>
    internal sealed class MockNetworkTimeBuilder
    {
        public event Action OnTimeReset;
        public event Action OnFixedNetworkUpdate;
        public event Action OnLateFixedNetworkUpdate;
        public event Action<AbsoluteSimulationFrame, AbsoluteSimulationFrame> OnServerSimulationFrameReceived;

        private double timeAsDouble;
        private double sessionTimeAsDouble;
        private double networkTimeScaleAsDouble = 1d;
        private double targetTimeScale = 1.0;
        private double fixedTimeStep = 0.02;
        private double maximumDeltaTime = 0.1;
        private bool multiClientMode;
        private bool accountForPing = true;
        private bool smoothTimeScaleChange = true;
        private bool pause;
        private bool isTimeSynced = true;
        private AbsoluteSimulationFrame clientSimulationFrame;
        private AbsoluteSimulationFrame clientFixedSimulationFrame;
        private AbsoluteSimulationFrame serverSimulationFrame;
        private AbsoluteSimulationFrame connectionSimulationFrame;

        private Mock<INetworkTime> mock;
        private INetworkTime networkTime;
        private bool buildExecuted;

        public Mock<INetworkTime> Mock
        {
            get
            {
                if (mock is null)
                {
                    Build();
                }

                return mock;
            }
        }

        public MockNetworkTimeBuilder SetTimeAsDouble(double time)
        {
            this.timeAsDouble = time;
            return this;
        }

        public MockNetworkTimeBuilder SetSessionTime(double sessionTime)
        {
            this.sessionTimeAsDouble = sessionTime;
            return this;
        }

        public MockNetworkTimeBuilder SetNetworkTimeScale(double timeScale)
        {
            this.networkTimeScaleAsDouble = timeScale;
            return this;
        }

        public MockNetworkTimeBuilder SetTargetTimeScale(double targetTimeScale)
        {
            this.targetTimeScale = targetTimeScale;
            return this;
        }

        public MockNetworkTimeBuilder SetFixedTimeStep(double fixedTimeStep)
        {
            this.fixedTimeStep = fixedTimeStep;
            return this;
        }

        public MockNetworkTimeBuilder SetMaximumDeltaTime(double maximumDeltaTime)
        {
            this.maximumDeltaTime = maximumDeltaTime;
            return this;
        }

        public MockNetworkTimeBuilder SetMultiClientMode(bool multiClientMode = true)
        {
            this.multiClientMode = multiClientMode;
            return this;
        }

        public MockNetworkTimeBuilder SetAccountForPing(bool accountForPing = true)
        {
            this.accountForPing = accountForPing;
            return this;
        }

        public MockNetworkTimeBuilder SetSmoothTimeScaleChange(bool smoothTimeScaleChange = true)
        {
            this.smoothTimeScaleChange = smoothTimeScaleChange;
            return this;
        }

        public MockNetworkTimeBuilder SetPause(bool pause = true)
        {
            this.pause = pause;
            return this;
        }

        public MockNetworkTimeBuilder SetIsTimeSynced(bool isTimeSynced = true)
        {
            this.isTimeSynced = isTimeSynced;
            return this;
        }

        public MockNetworkTimeBuilder SetClientSimulationFrame(AbsoluteSimulationFrame frame)
        {
            this.clientSimulationFrame = frame;
            return this;
        }

        public MockNetworkTimeBuilder SetClientFixedSimulationFrame(AbsoluteSimulationFrame frame)
        {
            this.clientFixedSimulationFrame = frame;
            return this;
        }

        public MockNetworkTimeBuilder SetServerSimulationFrame(AbsoluteSimulationFrame frame)
        {
            this.serverSimulationFrame = frame;
            return this;
        }

        public MockNetworkTimeBuilder SetConnectionSimulationFrame(AbsoluteSimulationFrame frame)
        {
            this.connectionSimulationFrame = frame;
            return this;
        }

        public INetworkTime Build()
        {
            if (buildExecuted)
            {
                return networkTime ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;
            mock = new(MockBehavior.Strict);
            SetupEvents();
            SetupProperties();
            SetupMethods();
            networkTime = mock.Object;
            return mock.Object;

            void SetupEvents()
            {
                mock.SetupAdd(nt => nt.OnTimeReset += It.IsAny<Action>()).Callback<Action>(handler => OnTimeReset += handler);
                mock.SetupRemove(nt => nt.OnTimeReset -= It.IsAny<Action>()).Callback<Action>(handler => OnTimeReset -= handler);
                mock.SetupAdd(nt => nt.OnFixedNetworkUpdate += It.IsAny<Action>()).Callback<Action>(handler => OnFixedNetworkUpdate += handler);
                mock.SetupRemove(nt => nt.OnFixedNetworkUpdate -= It.IsAny<Action>()).Callback<Action>(handler => OnFixedNetworkUpdate -= handler);
                mock.SetupAdd(nt => nt.OnLateFixedNetworkUpdate += It.IsAny<Action>()).Callback<Action>(handler => OnLateFixedNetworkUpdate += handler);
                mock.SetupRemove(nt => nt.OnLateFixedNetworkUpdate -= It.IsAny<Action>()).Callback<Action>(handler => OnLateFixedNetworkUpdate -= handler);
                mock.SetupAdd(nt => nt.OnServerSimulationFrameReceived += It.IsAny<Action<AbsoluteSimulationFrame, AbsoluteSimulationFrame>>()).Callback<Action<AbsoluteSimulationFrame, AbsoluteSimulationFrame>>(handler => OnServerSimulationFrameReceived += handler);
                mock.SetupRemove(nt => nt.OnServerSimulationFrameReceived -= It.IsAny<Action<AbsoluteSimulationFrame, AbsoluteSimulationFrame>>()).Callback<Action<AbsoluteSimulationFrame, AbsoluteSimulationFrame>>(handler => OnServerSimulationFrameReceived -= handler);
            }

            void SetupProperties()
            {
                mock.Setup(nt => nt.TimeAsDouble).Returns(() => timeAsDouble);
                mock.Setup(nt => nt.SessionTime).Returns(() => (float)sessionTimeAsDouble);
                mock.Setup(nt => nt.SessionTimeAsDouble).Returns(() => sessionTimeAsDouble);
                mock.Setup(nt => nt.NetworkTimeScale).Returns(() => (float)networkTimeScaleAsDouble);
                mock.Setup(nt => nt.NetworkTimeScaleAsDouble).Returns(() => networkTimeScaleAsDouble);
                mock.Setup(nt => nt.TargetTimeScale).Returns(() => targetTimeScale);
                mock.Setup(nt => nt.FixedTimeStep).Returns(() => fixedTimeStep);
                mock.SetupSet(nt => nt.FixedTimeStep = It.IsAny<double>()).Callback<double>(value => fixedTimeStep = value);
                mock.Setup(nt => nt.MaximumDeltaTime).Returns(() => maximumDeltaTime);
                mock.SetupSet(nt => nt.MaximumDeltaTime = It.IsAny<double>()).Callback<double>(value => maximumDeltaTime = value);
                mock.Setup(nt => nt.MultiClientMode).Returns(() => multiClientMode);
                mock.SetupSet(nt => nt.MultiClientMode = It.IsAny<bool>()).Callback<bool>(value => multiClientMode = value);
                mock.Setup(nt => nt.AccountForPing).Returns(() => accountForPing);
                mock.SetupSet(nt => nt.AccountForPing = It.IsAny<bool>()).Callback<bool>(value => accountForPing = value);
                mock.Setup(nt => nt.SmoothTimeScaleChange).Returns(() => smoothTimeScaleChange);
                mock.SetupSet(nt => nt.SmoothTimeScaleChange = It.IsAny<bool>()).Callback<bool>(value => smoothTimeScaleChange = value);
                mock.Setup(nt => nt.Pause).Returns(() => pause);
                mock.SetupSet(nt => nt.Pause = It.IsAny<bool>()).Callback<bool>(value => pause = value);
                mock.Setup(nt => nt.IsTimeSynced).Returns(() => isTimeSynced);
                mock.Setup(nt => nt.ClientSimulationFrame).Returns(() => clientSimulationFrame);
                mock.Setup(nt => nt.ClientFixedSimulationFrame).Returns(() => clientFixedSimulationFrame);
                mock.Setup(nt => nt.ServerSimulationFrame).Returns(() => serverSimulationFrame);
                mock.Setup(nt => nt.ConnectionSimulationFrame).Returns(() => connectionSimulationFrame);
            }

            void SetupMethods()
            {
                mock.Setup(x => x.Step(It.IsAny<double>(), It.IsAny<bool>())).Callback<double, bool>((currentTime, stopApplyingServerSimFrame) =>
                {
                    timeAsDouble = currentTime;
                    sessionTimeAsDouble = currentTime;
                });

                mock.Setup(x => x.Reset(It.IsAny<AbsoluteSimulationFrame>(), It.IsAny<bool>())).Callback<AbsoluteSimulationFrame, bool>((newFrame, notify) =>
                {
                    isTimeSynced = false;
                    clientSimulationFrame = newFrame;
                    clientFixedSimulationFrame = newFrame;
                    serverSimulationFrame = newFrame;
                    if (notify)
                    {
                        OnTimeReset?.Invoke();
                    }
                });
            }
        }

        /// <summary>
        /// Raise the <see cref="INetworkTime.OnFixedNetworkUpdate"/> event.
        /// </summary>
        public void RaiseFixedNetworkUpdate() => OnFixedNetworkUpdate?.Invoke();

        /// <summary>
        /// Raise the <see cref="INetworkTime.OnLateFixedNetworkUpdate"/> event.
        /// </summary>
        public void RaiseLateFixedNetworkUpdate() => OnLateFixedNetworkUpdate?.Invoke();

        /// <summary>
        /// Trigger the <see cref="INetworkTime.OnServerSimulationFrameReceived"/> event.
        /// </summary>
        public void RaiseServerSimulationFrameReceived(AbsoluteSimulationFrame current, AbsoluteSimulationFrame previous) => OnServerSimulationFrameReceived?.Invoke(current, previous);

        /// <summary>
        /// Trigger the <see cref="INetworkTime.OnTimeReset"/> event.
        /// </summary>
        public void RaiseTimeReset() => OnTimeReset?.Invoke();
    }
}
