// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.PingClient.Tests
{
    using System.Threading.Tasks;
    using Cloud;
    using NUnit.Framework;

    public class PingClientTests
    {
        [Test]
        public async Task PingAsync_RegionOverload_WithNullPingServers_FiltersThemOut()
        {
            var regions = new[]
            {
                new Region("us-east", null),
                new Region("eu-west", new PingServer { Region = "eu-west", Ip = "127.0.0.1", Port = 8080 }),
                new Region("ap-south", null)
            };

            var results = await PingClient.PingAsync(regions);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Region, Is.EqualTo("eu-west"));
        }

        [Test]
        public async Task PingAsync_RegionOverload_WithAllNullPingServers_ReturnsEmpty()
        {
            var regions = new[]
            {
                new Region("us-east", null),
                new Region("eu-west", null)
            };

            var results = await PingClient.PingAsync(regions);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task PingAsync_RegionOverload_WithAllValidPingServers_PingsAll()
        {
            var regions = new[]
            {
                new Region("us-east", new PingServer { Region = "us-east", Ip = "127.0.0.1", Port = 8080 }),
                new Region("eu-west", new PingServer { Region = "eu-west", Ip = "127.0.0.1", Port = 8081 })
            };

            var results = await PingClient.PingAsync(regions);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Region, Is.EqualTo("us-east"));
            Assert.That(results[1].Region, Is.EqualTo("eu-west"));
        }
    }
}
