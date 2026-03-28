// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests.Cloud.GameServices
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Common;
    using Moq;
    using NUnit.Framework;

    public class CloudCredentialsFactoryTests : CoherenceTest
    {
        private const string UniqueId = nameof(UniqueId);

        [Test]
        [Description(
            "Ensures that CloudCredentialsFactory creates valid credentials pair when provided with a player account provider.")]
        public void ForClient_WithPlayerAccountProvider_ReturnsCredentialsPair()
        {
            var runtimeSettings = new Mock<IRuntimeSettings>().Object;
            var playerAccountProvider = new Mock<IPlayerAccountProvider>().Object;
            var result = CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProvider);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.AuthClient, Is.Not.Null);
            Assert.That(result.RequestFactory, Is.Not.Null);

            // Dispose created credentials to avoid side effects on other tests.
            CloudCredentialsPair.Dispose(result.AuthClient, result.RequestFactory);
        }

        [Test]
        [Description(
            "Ensures that CloudCredentialsFactory creates valid credentials pair when provided with a unique ID.")]
        public void ForClient_WithUniqueId_ReturnsCredentialsPair()
        {
            var runtimeSettings = new Mock<IRuntimeSettings>().Object;
            var uniqueId = new CloudUniqueId(UniqueId);
            var result = CloudCredentialsFactory.ForClient(runtimeSettings, uniqueId);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.AuthClient, Is.Not.Null);
            Assert.That(result.RequestFactory, Is.Not.Null);

            // Dispose created credentials to avoid side effects on other tests.
            CloudCredentialsPair.Dispose(result.AuthClient, result.RequestFactory);
        }

        [Test]
        [Description("Ensures that CloudCredentialsFactory creates valid credentials pair with default parameters.")]
        public void ForClient_Default_ReturnsCredentialsPair()
        {
            var runtimeSettings = new Mock<IRuntimeSettings>().Object;
            var result = CloudCredentialsFactory.ForClient(runtimeSettings);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.AuthClient, Is.Not.Null);
            Assert.That(result.RequestFactory, Is.Not.Null);

            // Dispose created credentials to avoid side effects on other tests.
            CloudCredentialsPair.Dispose(result.AuthClient, result.RequestFactory);
        }

        [Test]
        [Description(
            "Ensures that CloudCredentialsFactory returns a shared credentials pair for simulators if a token is set.")]
        public void ForSimulator_ReturnsSharedCredentialsPair()
        {
            // Store original auth token and replace with a dummy token. Tokens are required to create shared credentials.
            var authToken = SimulatorUtility.AuthToken;
            SimulatorUtility.RemoveArgument(SimulatorUtility.AuthTokenKeyword);
            SimulatorUtility.SetArgument(SimulatorUtility.AuthTokenKeyword, nameof(authToken));

            var runtimeSettings = new Mock<IRuntimeSettings>().Object;
            var playerAccountProvider = new Mock<IPlayerAccountProvider>().Object;

            // First call creates and stores shared instance
            var result1 = CloudCredentialsFactory.ForSimulator(runtimeSettings, playerAccountProvider);
            // Second call returns the same instance
            var result2 = CloudCredentialsFactory.ForSimulator(runtimeSettings, playerAccountProvider);

            Assert.That(result1, Is.SameAs(result2));

            // Dispose created credentials to avoid side effects on other tests.
            CloudCredentialsPair.Dispose(result1.AuthClient, result1.RequestFactory);

            // Restore original auth token
            SimulatorUtility.RemoveArgument(SimulatorUtility.AuthTokenKeyword);
            SimulatorUtility.SetArgument(SimulatorUtility.AuthTokenKeyword, authToken);
        }

        [Test]
        [Description(
            "Ensures that CloudCredentialsFactory returns a unique credentials pair for simulators if an auth token is not set.")]
        public void ForSimulator_ReturnsUniqueCredentialsPair()
        {
            // Store original auth token and replace with a dummy token. Tokens are required to create shared credentials.
            var authToken = SimulatorUtility.AuthToken;
            SimulatorUtility.RemoveArgument(SimulatorUtility.AuthTokenKeyword);

            var runtimeSettings = new Mock<IRuntimeSettings>().Object;
            var playerAccountProvider = new Mock<IPlayerAccountProvider>().Object;

            // First call creates a unique instance
            var result1 = CloudCredentialsFactory.ForSimulator(runtimeSettings, playerAccountProvider);
            // Second call returns a different instance
            var result2 = CloudCredentialsFactory.ForSimulator(runtimeSettings, playerAccountProvider);

            Assert.That(result1, Is.Not.SameAs(result2));

            // Dispose created credentials to avoid side effects on other tests.
            CloudCredentialsPair.Dispose(result1.AuthClient, result1.RequestFactory);

            // Restore original auth token
            SimulatorUtility.RemoveArgument(SimulatorUtility.AuthTokenKeyword);
            SimulatorUtility.SetArgument(SimulatorUtility.AuthTokenKeyword, authToken);
        }
    }
}
