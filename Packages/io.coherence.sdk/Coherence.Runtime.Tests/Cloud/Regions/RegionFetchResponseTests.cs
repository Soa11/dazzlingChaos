// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests.Cloud.Regions
{
    using System;
    using Coherence.Cloud;
    using NUnit.Framework;

    public class RegionFetchResponseTests
    {
        [Test]
        public void ImplicitConversion_ToRegionArray_WithEmptyResponse_ReturnsEmptyArray()
        {
            var response = new RegionFetchResponse
            {
                Regions = null,
                PingServers = null
            };

            Region[] regions = response;

            Assert.That(regions, Is.Not.Null);
            Assert.That(regions.Length, Is.EqualTo(0));
        }

        [Test]
        public void ImplicitConversion_ToRegionArray_WithRegionsNoPingServers_CreatesRegionsWithoutPingServers()
        {
            var response = new RegionFetchResponse
            {
                Regions = new[] { "us", "eu", "ap" },
                PingServers = null
            };

            Region[] regions = response;

            Assert.That(regions.Length, Is.EqualTo(3));
            Assert.That(regions[0].Name, Is.EqualTo("us"));
            Assert.That(regions[0].PingServer, Is.Null);
            Assert.That(regions[1].Name, Is.EqualTo("eu"));
            Assert.That(regions[1].PingServer, Is.Null);
            Assert.That(regions[2].Name, Is.EqualTo("ap"));
            Assert.That(regions[2].PingServer, Is.Null);
        }

        [Test]
        public void ImplicitConversion_ToRegionArray_WithMatchingPingServers_AssociatesCorrectly()
        {
            var response = new RegionFetchResponse
            {
                Regions = new[] { "us", "eu" },
                PingServers = new[]
                {
                    new PingServer { Region = "eu", Ip = "5.6.7.8", Port = 8081 },
                    new PingServer { Region = "us", Ip = "1.2.3.4", Port = 8080 }
                }
            };

            Region[] regions = response;

            Assert.That(regions.Length, Is.EqualTo(2));
            Assert.That(regions[0].Name, Is.EqualTo("us"));
            Assert.That(regions[0].PingServer, Is.Not.Null);
            Assert.That(regions[0].PingServer.Value.Ip, Is.EqualTo("1.2.3.4"));
            Assert.That(regions[0].PingServer.Value.Port, Is.EqualTo(8080));
            Assert.That(regions[1].Name, Is.EqualTo("eu"));
            Assert.That(regions[1].PingServer, Is.Not.Null);
            Assert.That(regions[1].PingServer.Value.Ip, Is.EqualTo("5.6.7.8"));
            Assert.That(regions[1].PingServer.Value.Port, Is.EqualTo(8081));
        }

        [Test]
        public void ImplicitConversion_ToRegionArray_WithMismatchedPingServers_LeavesUnmatchedRegionsWithoutPingServer()
        {
            var response = new RegionFetchResponse
            {
                Regions = new[] { "us", "eu", "ap" },
                PingServers = new[]
                {
                    new PingServer { Region = "us", Ip = "1.2.3.4", Port = 8080 },
                    new PingServer { Region = "ap", Ip = "9.10.11.12", Port = 8082 }
                }
            };

            Region[] regions = response;

            Assert.That(regions.Length, Is.EqualTo(3));
            Assert.That(regions[0].Name, Is.EqualTo("us"));
            Assert.That(regions[0].PingServer, Is.Not.Null);
            Assert.That(regions[1].Name, Is.EqualTo("eu"));
            Assert.That(regions[1].PingServer, Is.Null);
            Assert.That(regions[2].Name, Is.EqualTo("ap"));
            Assert.That(regions[2].PingServer, Is.Not.Null);
        }

        [Test]
        public void ImplicitConversion_FromRegionArray_WithNullArray_ReturnsEmptyArrays()
        {
            Region[] regions = null;

            RegionFetchResponse response = regions;

            Assert.That(response.Regions, Is.Not.Null);
            Assert.That(response.Regions.Length, Is.EqualTo(0));
            Assert.That(response.PingServers, Is.Not.Null);
            Assert.That(response.PingServers.Length, Is.EqualTo(0));
        }

        [Test]
        public void ImplicitConversion_FromRegionArray_WithEmptyArray_ReturnsEmptyArrays()
        {
            var regions = Array.Empty<Region>();

            RegionFetchResponse response = regions;

            Assert.That(response.Regions, Is.Not.Null);
            Assert.That(response.Regions.Length, Is.EqualTo(0));
            Assert.That(response.PingServers, Is.Not.Null);
            Assert.That(response.PingServers.Length, Is.EqualTo(0));
        }

        [Test]
        public void ImplicitConversion_FromRegionArray_WithRegionsNoPingServers_ReturnsOnlyRegionNames()
        {
            var regions = new[]
            {
                new Region("us", null),
                new Region("eu", null),
                new Region("ap", null)
            };

            RegionFetchResponse response = regions;

            Assert.That(response.Regions, Is.Not.Null);
            Assert.That(response.Regions.Length, Is.EqualTo(3));
            Assert.That(response.Regions[0], Is.EqualTo("us"));
            Assert.That(response.Regions[1], Is.EqualTo("eu"));
            Assert.That(response.Regions[2], Is.EqualTo("ap"));
            Assert.That(response.PingServers, Is.Not.Null);
            Assert.That(response.PingServers.Length, Is.EqualTo(0));
        }

        [Test]
        public void ImplicitConversion_FromRegionArray_WithRegionsAndPingServers_ExtractsBoth()
        {
            var pingServer1 = new PingServer { Region = "us", Ip = "1.2.3.4", Port = 8080 };
            var pingServer2 = new PingServer { Region = "eu", Ip = "5.6.7.8", Port = 8081 };
            var regions = new[]
            {
                new Region("us", pingServer1),
                new Region("eu", pingServer2)
            };

            RegionFetchResponse response = regions;

            Assert.That(response.Regions, Is.Not.Null);
            Assert.That(response.Regions.Length, Is.EqualTo(2));
            Assert.That(response.Regions[0], Is.EqualTo("us"));
            Assert.That(response.Regions[1], Is.EqualTo("eu"));
            Assert.That(response.PingServers, Is.Not.Null);
            Assert.That(response.PingServers.Length, Is.EqualTo(2));
            Assert.That(response.PingServers[0].Region, Is.EqualTo("us"));
            Assert.That(response.PingServers[0].Ip, Is.EqualTo("1.2.3.4"));
            Assert.That(response.PingServers[0].Port, Is.EqualTo(8080));
            Assert.That(response.PingServers[1].Region, Is.EqualTo("eu"));
            Assert.That(response.PingServers[1].Ip, Is.EqualTo("5.6.7.8"));
            Assert.That(response.PingServers[1].Port, Is.EqualTo(8081));
        }

        [Test]
        public void ImplicitConversion_FromRegionArray_WithMixedPingServers_ExtractsOnlyValid()
        {
            var pingServer1 = new PingServer { Region = "us", Ip = "1.2.3.4", Port = 8080 };
            var regions = new[]
            {
                new Region("us", pingServer1),
                new Region("eu", null),
                new Region("ap", null)
            };

            RegionFetchResponse response = regions;

            Assert.That(response.Regions, Is.Not.Null);
            Assert.That(response.Regions.Length, Is.EqualTo(3));
            Assert.That(response.PingServers, Is.Not.Null);
            Assert.That(response.PingServers.Length, Is.EqualTo(1));
            Assert.That(response.PingServers[0].Region, Is.EqualTo("us"));
        }

        [Test]
        public void GetPingServersFromRegions_WithNull_ReturnsEmptyArray()
        {
            var result = RegionFetchResponse.GetPingServersFromRegions(null);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0));
        }

        [Test]
        public void GetPingServersFromRegions_WithEmptyArray_ReturnsEmptyArray()
        {
            var result = RegionFetchResponse.GetPingServersFromRegions(Array.Empty<Region>());

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0));
        }

        [Test]
        public void GetPingServersFromRegions_WithRegionsNoPingServers_ReturnsEmptyArray()
        {
            var regions = new[]
            {
                new Region("us", null),
                new Region("eu", null)
            };

            var result = RegionFetchResponse.GetPingServersFromRegions(regions);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0));
        }

        [Test]
        public void GetPingServersFromRegions_WithAllRegionsHavingPingServers_ReturnsAllPingServers()
        {
            var pingServer1 = new PingServer { Region = "us", Ip = "1.2.3.4", Port = 8080 };
            var pingServer2 = new PingServer { Region = "eu", Ip = "5.6.7.8", Port = 8081 };
            var regions = new[]
            {
                new Region("us", pingServer1),
                new Region("eu", pingServer2)
            };

            var result = RegionFetchResponse.GetPingServersFromRegions(regions);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].Region, Is.EqualTo("us"));
            Assert.That(result[0].Ip, Is.EqualTo("1.2.3.4"));
            Assert.That(result[0].Port, Is.EqualTo(8080));
            Assert.That(result[1].Region, Is.EqualTo("eu"));
            Assert.That(result[1].Ip, Is.EqualTo("5.6.7.8"));
            Assert.That(result[1].Port, Is.EqualTo(8081));
        }

        [Test]
        public void GetPingServersFromRegions_WithMixedPingServers_ReturnsOnlyValid()
        {
            var pingServer1 = new PingServer { Region = "us", Ip = "1.2.3.4", Port = 8080 };
            var pingServer3 = new PingServer { Region = "ap", Ip = "9.10.11.12", Port = 8082 };
            var regions = new[]
            {
                new Region("us", pingServer1),
                new Region("eu", null),
                new Region("ap", pingServer3),
                new Region("sa-east", null)
            };

            var result = RegionFetchResponse.GetPingServersFromRegions(regions);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].Region, Is.EqualTo("us"));
            Assert.That(result[1].Region, Is.EqualTo("ap"));
        }

        [Test]
        public void RoundTrip_ToRegionArray_AndBack_PreservesData()
        {
            var originalResponse = new RegionFetchResponse
            {
                Regions = new[] { "us", "eu" },
                PingServers = new[]
                {
                    new PingServer { Region = "us", Ip = "1.2.3.4", Port = 8080 },
                    new PingServer { Region = "eu", Ip = "5.6.7.8", Port = 8081 }
                }
            };

            Region[] regions = originalResponse;
            RegionFetchResponse roundTripResponse = regions;

            Assert.That(roundTripResponse.Regions.Length, Is.EqualTo(originalResponse.Regions.Length));
            Assert.That(roundTripResponse.PingServers.Length, Is.EqualTo(originalResponse.PingServers.Length));
            for (int i = 0; i < roundTripResponse.Regions.Length; i++)
            {
                Assert.That(roundTripResponse.Regions[i], Is.EqualTo(originalResponse.Regions[i]));
            }
            for (int i = 0; i < roundTripResponse.PingServers.Length; i++)
            {
                Assert.That(roundTripResponse.PingServers[i].Region, Is.EqualTo(originalResponse.PingServers[i].Region));
                Assert.That(roundTripResponse.PingServers[i].Ip, Is.EqualTo(originalResponse.PingServers[i].Ip));
                Assert.That(roundTripResponse.PingServers[i].Port, Is.EqualTo(originalResponse.PingServers[i].Port));
            }
        }
    }
}
