// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests.Cloud.Regions
{
    using Coherence.Cloud;
    using NUnit.Framework;

    public class PingServerTests
    {
        [Test]
        public void Validate_WithValidIPv4_ReturnsTrue()
        {
            var server = new PingServer { Region = "test", Ip = "127.0.0.1", Port = 8080 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void Validate_WithEmptyIp_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = "", Port = 8080 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("address"));
        }

        [Test]
        public void Validate_WithNullIp_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = null, Port = 8080 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("address"));
        }

        [Test]
        public void Validate_WithPortZero_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = "127.0.0.1", Port = 0 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("port").And.Contains($"{server.Port}"));
        }

        [Test]
        public void Validate_WithNegativePort_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = "127.0.0.1", Port = -1 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("port").And.Contains($"{server.Port}"));
        }

        [Test]
        public void Validate_WithInvalidIpFormat_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = "not-an-ip", Port = 8080 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("address").And.Contains(server.Ip));
        }

        [Test]
        public void Validate_WithDomainName_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = "example.com", Port = 8080 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("address").And.Contains(server.Ip));
        }

        [Test]
        public void Validate_WithIPv6Address_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = "::1", Port = 8080 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("address").And.Contains(server.Ip));
        }

        [Test]
        public void Validate_WithFullIPv6Address_ReturnsFalse()
        {
            var server = new PingServer { Region = "test", Ip = "2001:0db8:85a3:0000:0000:8a2e:0370:7334", Port = 8080 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("address").And.Contains(server.Ip));
        }

        [Test]
        public void Validate_WithMaxPortValue_ReturnsTrue()
        {
            var server = new PingServer { Region = "test", Ip = "192.168.1.1", Port = 65535 };

            var isValid = server.Validate(out var errorMessage);

            Assert.That(isValid, Is.True);
            Assert.That(errorMessage, Is.Null);
        }
    }
}
