// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using Coherence.Tests;
    using Connection;
    using NUnit.Framework;

    public class EndPointDataTests : CoherenceTest
    {
        [Test]
        [Description("Validates that local EndPointData instances are correctly identified as valid")]
        [TestCase(42001, "schemaid")]
        public void Valid_Local_EndPointData(int port, string schemaId)
        {
            var localIpAddress = GetLocalIpAddress();

            var endPoint = new EndpointData
            {
                host = localIpAddress,
                port = port,
                schemaId = schemaId,
                region = EndpointData.LocalRegion
            };

            var validation = endPoint.ValidateLocalAddress();
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.Host), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.Port), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.SchemaId), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.AuthToken), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.ValidEndpoint), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.ValidLocalEndpoint), Is.True);
        }

        [Test]
        [Description("Flags should be set correctly based on endpoint parameters")]
        [TestCase("127.0.0.1", 42002, "", "", EndpointData.ValidationResult.Port)]
        [TestCase(null, 0, "schemaid", "", EndpointData.ValidationResult.SchemaId)]
        [TestCase(null, 0, "", "authtoken", EndpointData.ValidationResult.AuthToken)]
        public void Flags_Should_Be_Set_Correctly_Based_On_Parameters(string host, int port, string schemaId, string authToken, EndpointData.ValidationResult requiredFlag)
        {
            var localIpAddress = GetLocalIpAddress();

            var endPoint = new EndpointData
            {
                host = host ?? localIpAddress,
                port = port,
                schemaId = schemaId,
                authToken = authToken
            };

            var validation = endPoint.ValidateLocalAddress();
            Assert.That(validation.HasFlag(requiredFlag), Is.True);

            var message = endPoint.GetErrorMessage(validation);
            Assert.That(message, Is.Not.Empty.Or.Null);
        }

        [Test]
        [Description("Validates that EndPointData instances are correctly identified as valid")]
        [TestCase("8.8.8.8", 42001, "schemaid")]
        [TestCase("1.2.3.4", 42002, "schemaid")]
        [TestCase("127.0.0.200", 42003, "schemaid")]
        public void Invalid_Local_EndPointData(string host, int port, string schemaId)
        {
            var endPoint = new EndpointData
            {
                host = host,
                port = port,
                schemaId = schemaId,
                region = EndpointData.LocalRegion
            };

            var validation = endPoint.ValidateLocalAddress();
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.Host), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.Port), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.SchemaId), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.ValidEndpoint), Is.True);
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.ValidLocalEndpoint), Is.False);
        }

        [Test]
        [Description("Valid regions do not set the AuthToken flag")]
        [TestCase(42001, "schemaid", "", EndpointData.LocalRegion)]
        public void Valid_Region_Set_For_AuthToken(int port, string schemaId, string authToken, string region)
        {
            var localIpAddress = GetLocalIpAddress();

            var endPoint = new EndpointData
            {
                host = localIpAddress,
                port = port,
                schemaId = schemaId,
                authToken = authToken,
                region = region
            };

            var validation = endPoint.ValidateLocalAddress();
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.AuthToken), Is.True);
        }

        [Test]
        [Description("Invalid regions do set the AuthToken flag but shouldn't have LocalRegion flag")]
        [TestCase(42001, "schemaid", "authtoken", "")]
        [TestCase(42001, "schemaid", "authtoken", "world")]
        public void Invalid_Region_Set_For_AuthToken(int port, string schemaId, string authToken, string region)
        {
            var localIpAddress = GetLocalIpAddress();

            var endPoint = new EndpointData
            {
                host = localIpAddress,
                port = port,
                schemaId = schemaId,
                authToken = authToken,
                region = region
            };

            var validation = endPoint.ValidateLocalAddress();
            Assert.That(validation.HasFlag(EndpointData.ValidationResult.AuthToken), Is.True);
        }

        private string GetLocalIpAddress() => NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .FirstOrDefault();
    }
}
