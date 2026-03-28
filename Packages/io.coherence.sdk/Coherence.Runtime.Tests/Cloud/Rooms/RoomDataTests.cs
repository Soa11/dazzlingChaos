// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests.Cloud.Rooms
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Connection;
    using NUnit.Framework;

    public class RoomDataTests : CoherenceTest
    {
        [Test]
        [Description("Tests that EndpointData contains correct data from RoomData.")]
        public void EndpointData_Contains_Correct_Data_From_RoomData()
        {
            var roomData = new RoomData()
            {
                Id = 2345,
                UniqueId = 1234,
                Host = new RoomHostData()
                {
                    Ip = "127.0.0.1",
                    Port = 9000,
                    Region = "eu"
                },
                Secret = "its-a-secret",
                SimSlug = "sim-slug",
                AuthToken = "auth-token"
            };

            var (endPoint, isValid, validationErrorMessage) = RoomData.GetRoomEndpointData(roomData);

            Assert.That(isValid, Is.True);
            Assert.That(validationErrorMessage, Is.Null.Or.Empty);

            Assert.That(endPoint.host, Is.EqualTo(roomData.Host.Ip));
            Assert.That(endPoint.port, Is.EqualTo(roomData.Host.Port));
            Assert.That(endPoint.roomId, Is.EqualTo(roomData.Id));
            Assert.That(endPoint.uniqueRoomId, Is.EqualTo(roomData.UniqueId));
            Assert.That(endPoint.runtimeKey, Is.EqualTo(RuntimeSettings.Instance.RuntimeKey));
            Assert.That(endPoint.schemaId, Is.EqualTo(RuntimeSettings.Instance.SchemaID));
            Assert.That(endPoint.region, Is.EqualTo(roomData.Host.Region));
            Assert.That(endPoint.authToken, Is.EqualTo(roomData.AuthToken).Or.EqualTo(SimulatorUtility.AuthToken));
            Assert.That(endPoint.roomSecret, Is.EqualTo(roomData.Secret));
            Assert.That(endPoint.simulatorType, Is.EqualTo(nameof(EndpointData.SimulatorType.room)));
        }

        [Test]
        [Description("Tests that GetRoomEndpointData returns correct data for valid RoomData.")]
        public void Empty_Ip_Address_Returns_Error_Message_From_GetRoomEndpointData()
        {
            var roomData = new RoomData()
            {
                Id = 2345,
                UniqueId = 1234,
                Host = new RoomHostData()
                {
                    Ip = "",
                    Port = 9000,
                    Region = "local"
                },
                Secret = "its-a-secret",
                SimSlug = "sim-slug",
                AuthToken = "auth-token"
            };

            var (_, isValid, validationErrorMessage) = RoomData.GetRoomEndpointData(roomData);

            Assert.That(isValid, Is.False);
            Assert.That(validationErrorMessage, Is.Not.Null.Or.Empty);
        }

        [Test]
        [Description("Tests that a copy of RoomData is equal to the original.")]
        public void RoomData_Copy_Of_RoomData_Is_Equal()
        {
            var roomData = new RoomData()
            {
                UniqueId = 1234,
                Host = new RoomHostData()
                {
                    Ip = "127.0.0.1",
                    Port = 9000
                },
                Secret = "its-a-secret",
                SimSlug = "sim-slug",
            };

            var roomDataCopy = new RoomData()
            {
                UniqueId = 1234,
                Host = new RoomHostData()
                {
                    Ip = "127.0.0.1",
                    Port = 9000
                },
                Secret = "its-a-secret",
                SimSlug = "sim-slug",
            };

            Assert.That(roomDataCopy.Equals(roomData), Is.True);
        }

        [Test]
        [Description("Tests that RoomData.ToString() contains the correct information.")]
        public void RoomData_ToString_Contains_Correct_Information()
        {
            var roomData = new RoomData()
            {
                Id = 1234,
                Host = new RoomHostData()
                {
                    Ip = "127.0.0.1",
                    Port = 9000
                },
                Secret = "its-a-secret",
                SimSlug = "sim-slug",
                ConnectedPlayers = 4,
                MaxPlayers = 16,
            };

            var visual = roomData.ToString();
            Assert.That(visual, Contains.Substring(roomData.Host.Ip));
            Assert.That(visual, Contains.Substring(roomData.Host.Port.ToString()));
            Assert.That(visual, Contains.Substring(roomData.Id.ToString()));
            Assert.That(visual, Contains.Substring(roomData.ConnectedPlayers.ToString()));
            Assert.That(visual, Contains.Substring(roomData.MaxPlayers.ToString()));
        }
    }
}
