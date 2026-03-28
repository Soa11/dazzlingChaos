namespace Coherence.Runtime.Tests.Cloud.Rooms
{
    using Coherence;
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;
    using HostData = Coherence.Cloud.HostData;

    public class WorldDataTests : CoherenceTest
    {
        private const string Localhost = "127.0.0.1";
        private const string LocalRegion = "local";
        private const string LocalWorld = "LocalWorld";
        private const string ItsASecret = "its-a-secret";
        private const string WorldDataName = "world-name";
        private const int WorldId = 123;
        private const int SigPort = 987;
        private const int WebPort = 80;
        private const int UdpPort = 765;

        [Test]
        [Description("Tests that WorldData can be serialized and deserialized correctly.")]
        public void WorldData_Serialization_RoundTrip()
        {
            var worldData = new WorldData()
            {
                AuthToken = "auth-token",
                Host = new HostData()
                {
                    Ip = Localhost,
                    SigPort = SigPort,
                    WebPort = WebPort,
                    SigURL = "myurl/endpoint",
                    UDPPort = UdpPort,
                    Region = LocalRegion
                },
                Name = WorldDataName,
                Region = LocalRegion,
                RoomSecret = ItsASecret,
            };

            var serialized = JsonUtility.ToJson(worldData);
            var deserialized = JsonUtility.FromJson<WorldData>(serialized);

            Assert.That(worldData.AuthToken, Is.EqualTo(deserialized.AuthToken));
            Assert.That(worldData.Host.Ip, Is.EqualTo(deserialized.Host.Ip));
            Assert.That(worldData.Host.SigPort, Is.EqualTo(deserialized.Host.SigPort));
            Assert.That(worldData.Host.WebPort, Is.EqualTo(deserialized.Host.WebPort));
            Assert.That(worldData.Host.SigURL, Is.EqualTo(deserialized.Host.SigURL));
            Assert.That(worldData.Host.UDPPort, Is.EqualTo(deserialized.Host.UDPPort));
            Assert.That(worldData.Host.Region, Is.EqualTo(deserialized.Host.Region));
            Assert.That(worldData.Name, Is.EqualTo(deserialized.Name));
            Assert.That(worldData.Region, Is.EqualTo(deserialized.Region));
            Assert.That(worldData.RoomSecret, Is.EqualTo(deserialized.RoomSecret));
        }

        [Test]
        [Description("Tests that WorldData.ToString() contains all fields.")]
        public void WorldData_ToString_Contains_All_Fields()
        {
            var worldData = new WorldData()
            {
                AuthToken = "auth-token",
                Host = new HostData()
                {
                    Ip = Localhost,
                    SigPort = SigPort,
                    WebPort = WebPort,
                    SigURL = "myurl/endpoint",
                    UDPPort = UdpPort,
                    Region = LocalRegion
                },
                Name = WorldDataName,
                Region = LocalRegion,
                RoomSecret = ItsASecret,
            };

            var toString = worldData.ToString();
            Assert.That(toString, Contains.Substring(worldData.Host.Ip));
            Assert.That(toString, Contains.Substring(worldData.Host.UDPPort.ToString()));
            Assert.That(toString, Contains.Substring(worldData.WorldId.ToString()));
        }

        [Test]
        [Description("Tests that WorldData generates a valid Endpoint from valid data.")]
        public void WorldData_Generates_Valid_Endpoint_From_Valid_Data()
        {
            var worldData = new WorldData()
            {
                AuthToken = "auth-token",
                Host = new HostData()
                {
                    Ip = Localhost,
                    SigPort = SigPort,
                    WebPort = WebPort,
                    SigURL = "myurl/endpoint",
                    UDPPort = UdpPort,
                    Region = LocalRegion
                },
                Name = WorldDataName,
                Region = LocalRegion,
                RoomSecret = ItsASecret,
            };

            var (endpointData, isValid, validationErrorMessage) = WorldData.GetWorldEndpoint(worldData);

            Assert.That(isValid, Is.True);
            Assert.That(validationErrorMessage, Is.Null.Or.Empty);

            Assert.That(endpointData.host, Is.EqualTo(worldData.Host.Ip));
            Assert.That(endpointData.port, Is.EqualTo(RuntimeSettings.Instance.LocalWorldUDPPort));
            Assert.That(endpointData.worldId, Is.EqualTo(worldData.WorldId));
            Assert.That(endpointData.region, Is.EqualTo(worldData.Host.Region));
            Assert.That(endpointData.roomSecret, Is.EqualTo(worldData.RoomSecret));
        }

        [Test]
        [Description("Tests that WorldData does not generate a valid Endpoint from invalid data.")]
        public void WorldData_Does_Not_Generate_Valid_Endpoint_From_Invalid_Data()
        {
            var worldData = new WorldData()
            {
                AuthToken = "auth-token",
                Host = new HostData()
                {
                    Ip = string.Empty,
                    SigPort = SigPort,
                    WebPort = WebPort,
                    SigURL = "myurl/endpoint",
                    UDPPort = UdpPort,
                    Region = LocalRegion
                },
                Name = WorldDataName,
                Region = LocalRegion,
                RoomSecret = ItsASecret,
            };

            var (_, isValid, validationErrorMessage) = WorldData.GetWorldEndpoint(worldData);

            Assert.That(isValid, Is.False);
            Assert.That(validationErrorMessage, Is.Not.Null.Or.Empty);
        }

        [Test]
        [Description("Tests that WorldData.GetLocalWorld returns valid WorldData.")]
        public void GetLocalWorld_Returns_Valid_WorldData()
        {
            var localWorld = WorldData.GetLocalWorld(Localhost);

            Assert.That(localWorld.WorldId, Is.EqualTo(WorldId));
            Assert.That(localWorld.Name, Is.EqualTo(LocalWorld));
            Assert.That(localWorld.Host.Ip, Is.EqualTo(Localhost));
            Assert.That(localWorld.Host.Region, Is.EqualTo(LocalRegion));
            Assert.That(localWorld.Region, Is.EqualTo(LocalRegion));
            Assert.That(localWorld.RoomSecret, Is.EqualTo(string.Empty));
        }
    }
}
