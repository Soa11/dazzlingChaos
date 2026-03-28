namespace Coherence.Runtime.Tests.Cloud.Rooms
{
    using System.Collections.Generic;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class RoomRequestsTests : CoherenceTest
    {
        [Test]
        [Description("Test that a RoomCreationRequest can be serialized and deserialized correctly.")]
        public void RoomCreationRequest_Serialization_RoundTrip()
        {
            var request = new RoomCreationRequest
            {
                Tags = new[]
                {
                    "tag1",
                    "tag2"
                },
                KV = new Dictionary<string, string>()
                {
                    { "key", "value" }
                },
                Region = "eu",
                SimSlug = "sim-slug",
                MaxClients = 65,
                FindOrCreate = true,
            };

            var serialized = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<RoomCreationRequest>(serialized);

            Assert.That(deserialized.Tags.Length, Is.EqualTo(request.Tags.Length));
            foreach (var tag in request.Tags)
            {
                Assert.That(deserialized.Tags, Does.Contain(tag));
            }

            Assert.That(deserialized.KV.Count, Is.EqualTo(request.KV.Count));
            foreach (var key in request.KV.Keys)
            {
                Assert.That(deserialized.KV.ContainsKey(key), Is.True);
                Assert.That(deserialized.KV[key], Is.EqualTo(request.KV[key]));
            }

            Assert.That(deserialized.Region, Is.EqualTo(request.Region));
            Assert.That(deserialized.SimSlug, Is.EqualTo(request.SimSlug));
            Assert.That(deserialized.MaxClients, Is.EqualTo(request.MaxClients));
            Assert.That(deserialized.FindOrCreate, Is.EqualTo(request.FindOrCreate));
        }

        [Test]
        [Description("Test that a RoomMatchRequest can be serialized and deserialized correctly.")]
        public void RoomMatchRequest_Serialization_RoundTrip()
        {
            var request = new RoomMatchRequest
            {
                Tags = new[]
                {
                    "tag1",
                    "tag2"
                },
                Region = "eu",
                SimSlug = "sim-slug",
            };

            var serialized = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<RoomMatchRequest>(serialized);

            Assert.That(deserialized.Tags.Length, Is.EqualTo(request.Tags.Length));
            foreach (var tag in request.Tags)
            {
                Assert.That(deserialized.Tags, Does.Contain(tag));
            }

            Assert.That(deserialized.Region, Is.EqualTo(request.Region));
            Assert.That(deserialized.SimSlug, Is.EqualTo(request.SimSlug));
        }

        [Test]
        [Description("Test that a RoomUnlistRequest can be serialized and deserialized correctly.")]
        public void RoomUnlistRequest_Serialization_RoundTrip()
        {
            var request = new RoomUnlistRequest
            {
                Secret = "room-secret",
            };

            var serialized = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<RoomUnlistRequest>(serialized);

            Assert.That(deserialized.Secret, Is.EqualTo(request.Secret));
        }

        [Test]
        [Description("Test that a RegionFetchResponse can be serialized and deserialized correctly.")]
        public void RegionFetchResponse_Serialization_RoundTrip()
        {
            var request = new RegionFetchResponse
            {
                Regions = new[]
                {
                    "eu",
                    "us"
                },
            };

            var serialized = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<RegionFetchResponse>(serialized);

            Assert.That(deserialized.Regions.Length, Is.EqualTo(request.Regions.Length));
            foreach (var region in request.Regions)
            {
                Assert.That(deserialized.Regions, Does.Contain(region));
            }
        }

        [Test]
        [Description("Test that a RoomMatchResponse can be serialized and deserialized correctly.")]
        public void RoomMatchResponse_Serialization_RoundTrip()
        {
            var room = new RoomData
            {
                Id = 16,
                UniqueId = 1024,
                MaxPlayers = 64,
            };

            var request = new RoomMatchResponse
            {
                Room = room
            };

            var serialized = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<RoomMatchResponse>(serialized);

            Assert.That(deserialized.Room, Is.Not.Null);
            Assert.That(deserialized.Room?.Id, Is.EqualTo(room.Id));
            Assert.That(deserialized.Room?.UniqueId, Is.EqualTo(room.UniqueId));
            Assert.That(deserialized.Room?.MaxPlayers, Is.EqualTo(room.MaxPlayers));
        }

        [Test]
        [Description("Test that a LocalRoomCreationRequest can be serialized and deserialized correctly.")]
        public void LocalRoomCreationRequest_Serialization_RoundTrip()
        {
            var request = new LocalRoomCreationRequest
            {
                UniqueID = 90210,
                MaxClients = 64,
                MaxEntities = 32768,
                OutStatsFreq = 60,
                LogStatsFreq = 30,
                SchemaName = "local",
                SchemaTimeout = 9000,
                SchemaUrls = new[]
                {
                    "url1",
                    "url2"
                },
                Schemas = new[]
                {
                    "de0a8",
                    "effb1",
                    "a39de"
                },
                DisconnectTimeout = 6000,
                DebugStreams = true,
                Frequency = 0,
                MinQueryDistance = 2.5f,
                WebSupport = true,
                CleanupTimeout = 60,
                ProjectID = "project-id",
                KeyValues = new Dictionary<string, string>
                {
                    { "key", "value" }
                },
                Tags = new[]
                {
                    "tag1",
                    "tag2"
                },
                Secret = "itsasecret",
                HostAuthority = "host-authority",
            };

            var serialized = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<LocalRoomCreationRequest>(serialized);

            Assert.That(deserialized.UniqueID, Is.EqualTo(request.UniqueID));
            Assert.That(deserialized.MaxClients, Is.EqualTo(request.MaxClients));
            Assert.That(deserialized.MaxEntities, Is.EqualTo(request.MaxEntities));
            Assert.That(deserialized.OutStatsFreq, Is.EqualTo(request.OutStatsFreq));
            Assert.That(deserialized.LogStatsFreq, Is.EqualTo(request.LogStatsFreq));
            Assert.That(deserialized.SchemaName, Is.EqualTo(request.SchemaName));
            Assert.That(deserialized.SchemaTimeout, Is.EqualTo(request.SchemaTimeout));

            Assert.That(deserialized.SchemaUrls, Is.EquivalentTo(request.SchemaUrls));
            foreach (var url in request.SchemaUrls)
            {
                Assert.That(deserialized.SchemaUrls, Does.Contain(url));
            }

            Assert.That(deserialized.Schemas, Is.EquivalentTo(request.Schemas));
            foreach (var schema in request.Schemas)
            {
                Assert.That(deserialized.Schemas, Does.Contain(schema));
            }

            Assert.That(deserialized.DisconnectTimeout, Is.EqualTo(request.DisconnectTimeout));
            Assert.That(deserialized.DebugStreams, Is.EqualTo(request.DebugStreams));
            Assert.That(deserialized.Frequency, Is.EqualTo(request.Frequency));
            Assert.That(deserialized.MinQueryDistance, Is.EqualTo(request.MinQueryDistance));
            Assert.That(deserialized.WebSupport, Is.EqualTo(request.WebSupport));
            Assert.That(deserialized.CleanupTimeout, Is.EqualTo(request.CleanupTimeout));
            Assert.That(deserialized.ProjectID, Is.EqualTo(request.ProjectID));

            Assert.That(deserialized.KeyValues.Count, Is.EqualTo(request.KeyValues.Count));
            foreach (var kv in request.KeyValues)
            {
                Assert.That(deserialized.KeyValues.ContainsKey(kv.Key), Is.True);
                Assert.That(deserialized.KeyValues[kv.Key], Is.EqualTo(kv.Value));
            }

            Assert.That(deserialized.Tags, Is.EquivalentTo(request.Tags));
            foreach (var tag in request.Tags)
            {
                Assert.That(deserialized.Tags, Does.Contain(tag));
            }

            Assert.That(deserialized.Secret, Is.EqualTo(request.Secret));
            Assert.That(deserialized.HostAuthority, Is.EqualTo(request.HostAuthority));
        }

        [Test]
        [Description("Test that a RemoveRoomRequest can be serialized and deserialized correctly.")]
        public void RemoveRoomRequest_Serialization_RoundTrip()
        {
            var request = new RemoveRoomRequest
            {
                RoomId = 12345,
            };

            var serialized = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<RemoveRoomRequest>(serialized);

            Assert.That(deserialized.RoomId, Is.EqualTo(request.RoomId));
        }

        [Test]
        [Description("Test that a LocalRoomData can be serialized and deserialized correctly.")]
        public void LocalRoomData_Serialization_RoundTrip()
        {
            var room = new LocalRoomData
            {
                RoomID = 1234,
                Secret = "its-a-secret",
            };

            var serialized = JsonConvert.SerializeObject(room);
            var deserialized = JsonConvert.DeserializeObject<LocalRoomData>(serialized);

            Assert.That(deserialized.RoomID, Is.EqualTo(room.RoomID));
            Assert.That(deserialized.Secret, Is.EqualTo(room.Secret));
        }

        [Test]
        [Description("Test that a LocalRoomsListItem can be serialized and deserialized correctly.")]
        public void LocalRoomsListItem_Serialization_RoundTrip()
        {
            var roomsListItem = new LocalRoomsListItem
            {
                UniqueID = 5678,
                ID = 32000,
                MaxClients = 64,
                SchemaName = "local-schema",
                ConnectionCount = 1024,
                LastCheckTime = "last-check-time",
                ProjectID = "project-id",
                KVP = new Dictionary<string, string>
                {
                    { "key", "value" }
                },
                Tags = new[]
                {
                    "tag1",
                    "tag2"
                },
            };

            var serialized = JsonConvert.SerializeObject(roomsListItem);
            var deserialized = JsonConvert.DeserializeObject<LocalRoomsListItem>(serialized);

            Assert.That(deserialized.UniqueID, Is.EqualTo(roomsListItem.UniqueID));
            Assert.That(deserialized.ID, Is.EqualTo(roomsListItem.ID));
            Assert.That(deserialized.MaxClients, Is.EqualTo(roomsListItem.MaxClients));
            Assert.That(deserialized.SchemaName, Is.EqualTo(roomsListItem.SchemaName));
            Assert.That(deserialized.ConnectionCount, Is.EqualTo(roomsListItem.ConnectionCount));
            Assert.That(deserialized.LastCheckTime, Is.EqualTo(roomsListItem.LastCheckTime));
            Assert.That(deserialized.ProjectID, Is.EqualTo(roomsListItem.ProjectID));

            Assert.That(deserialized.KVP.Count, Is.EqualTo(roomsListItem.KVP.Count));
            foreach (var kv in roomsListItem.KVP)
            {
                Assert.That(deserialized.KVP.ContainsKey(kv.Key), Is.True);
                Assert.That(deserialized.KVP[kv.Key], Is.EqualTo(kv.Value));
            }

            Assert.That(deserialized.Tags, Is.EquivalentTo(roomsListItem.Tags));
            foreach (var tag in roomsListItem.Tags)
            {
                Assert.That(deserialized.Tags, Does.Contain(tag));
            }
        }

        [Test]
        [Description("Test that a LocalRoomsResponse can be serialized and deserialized correctly.")]
        public void LocalRoomsResponse_Serialization_RoundTrip()
        {
            var room1 = new LocalRoomsListItem
            {
                UniqueID = 5678,
                ID = 32000,
                MaxClients = 64,
                SchemaName = "local-schema",
                ConnectionCount = 1024,
                LastCheckTime = "last-check-time",
                ProjectID = "project-id",
                KVP = new Dictionary<string, string>
                {
                    { "key", "value" }
                },
                Tags = new[]
                {
                    "tag1",
                    "tag2"
                },
            };

            var room2 = new LocalRoomsListItem
            {
                UniqueID = 6789,
                ID = 32001,
                MaxClients = 128,
                SchemaName = "another-schema",
                ConnectionCount = 2048,
                LastCheckTime = "another-check-time",
                ProjectID = "another-project-id",
                KVP = new Dictionary<string, string>
                {
                    { "another-key", "another-value" }
                },
                Tags = new[]
                {
                    "tag3",
                    "tag4"
                },
            };

            var response = new LocalRoomsResponse
            {
                Rooms = new[]
                {
                    room1,
                    room2
                },
            };

            var serialized = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<LocalRoomsResponse>(serialized);

            Assert.That(deserialized.Rooms.Length, Is.EqualTo(response.Rooms.Length));
            for (var i = 0; i < response.Rooms.Length; i++)
            {
                var originalRoom = response.Rooms[i];
                var deserializedRoom = deserialized.Rooms[i];

                Assert.That(deserializedRoom.UniqueID, Is.EqualTo(originalRoom.UniqueID));
                Assert.That(deserializedRoom.ID, Is.EqualTo(originalRoom.ID));
                Assert.That(deserializedRoom.MaxClients, Is.EqualTo(originalRoom.MaxClients));
                Assert.That(deserializedRoom.SchemaName, Is.EqualTo(originalRoom.SchemaName));
                Assert.That(deserializedRoom.ConnectionCount, Is.EqualTo(originalRoom.ConnectionCount));
                Assert.That(deserializedRoom.LastCheckTime, Is.EqualTo(originalRoom.LastCheckTime));
                Assert.That(deserializedRoom.ProjectID, Is.EqualTo(originalRoom.ProjectID));

                Assert.That(deserializedRoom.KVP.Count, Is.EqualTo(originalRoom.KVP.Count));
                foreach (var kv in originalRoom.KVP)
                {
                    Assert.That(deserializedRoom.KVP.ContainsKey(kv.Key), Is.True);
                    Assert.That(deserializedRoom.KVP[kv.Key], Is.EqualTo(kv.Value));
                }
            }
        }

        [Test]
        [Description("Test that a RoomHostData can be serialized and deserialized correctly.")]
        public void RoomHostData_Serialization_RoundTrip()
        {
            var roomHostData = new RoomHostData
            {
                Ip = "127.0.0.1",
                Port = 7777,
                Region = "us",
                RSVersion = "1.2.3"
            };

            var serialized = JsonConvert.SerializeObject(roomHostData);
            var deserialized = JsonConvert.DeserializeObject<RoomHostData>(serialized);

            Assert.That(deserialized.Equals(roomHostData), Is.True);

            Assert.That(deserialized.Ip, Is.EqualTo(roomHostData.Ip));
            Assert.That(deserialized.Port, Is.EqualTo(roomHostData.Port));
            Assert.That(deserialized.Region, Is.EqualTo(roomHostData.Region));
            Assert.That(deserialized.RSVersion, Is.EqualTo(roomHostData.RSVersion));

            var visual = deserialized.ToString();
            Assert.That(visual, Contains.Substring(roomHostData.Ip));
            Assert.That(visual, Contains.Substring(roomHostData.Port.ToString()));
            Assert.That(visual, Contains.Substring(roomHostData.Region));
            Assert.That(visual, Contains.Substring(roomHostData.RSVersion));
        }

        [Test]
        [Description("Test that RoomHostData equality and inequality checks work correctly.")]
        public void RoomHostData_Inequality_Check()
        {
            var roomHostData = new RoomHostData
            {
                Ip = "127.0.0.1",
                Port = 7777,
                Region = "us",
                RSVersion = "1.2.3"
            };

            var roomHostDataDuplicate = new RoomHostData
            {
                Ip = "127.0.0.1",
                Port = 7777,
                Region = "us",
                RSVersion = "1.2.3"
            };

            var roomHostDataDifferent = new RoomHostData
            {
                Ip = "127.0.0.2",
                Port = 6543,
                Region = "eu",
                RSVersion = "1.2.3"
            };

            Assert.That(roomHostData.Equals(roomHostDataDuplicate), Is.True);
            Assert.That(roomHostData.Equals(roomHostDataDifferent), Is.False);
        }

        [Test]
        [Description("Test that RoomCreationOptions sets the Name property correctly from KeyValues.")]
        public void RoomCreationOptions_Name_Property_Set_Correctly()
        {
            var roomCreationOptions = new RoomCreationOptions
            {
                Tags = new[]
                {
                    "tag1",
                    "tag2"
                },
                KeyValues = new Dictionary<string, string>()
                {
                    { "name", "my-room-name" }
                },
                SimPayload = "sim-payload",
                FindOrCreate = true,
            };

            Assert.That(roomCreationOptions.Name, Is.EqualTo(roomCreationOptions.KeyValues["name"]));
            Assert.That(roomCreationOptions.MaxClients, Is.Not.Zero);
        }

        [Test]
        [Description("Test that SelfHostedRoomCreationOptions creates a correct LocalRoomCreationRequest.")]
        public void SelfHostedRoomCreationOptions_Creates_LocalRoomCreationRequest_Correctly()
        {
            var roomCreationOptions = new SelfHostedRoomCreationOptions
            {
                Tags = new[]
                {
                    "tag1",
                    "tag2"
                },
                KeyValues = new Dictionary<string, string>()
                {
                    { "name", "my-self-hosted-room-name" }
                },
                SimPayload = "sim-payload",
                FindOrCreate = true,
            };

            var localRoomCreationRequest = roomCreationOptions.ToRequest();

            Assert.That(localRoomCreationRequest.UniqueID, Is.EqualTo(roomCreationOptions.UniqueId));
            Assert.That(localRoomCreationRequest.MaxClients, Is.EqualTo(roomCreationOptions.MaxClients));
            Assert.That(localRoomCreationRequest.MaxEntities, Is.EqualTo(roomCreationOptions.MaxEntities));
            Assert.That(localRoomCreationRequest.SchemaName, Is.Empty);
            Assert.That(localRoomCreationRequest.SchemaTimeout, Is.EqualTo(60));
            Assert.That(localRoomCreationRequest.SchemaUrls, Is.Empty);

            Assert.That(localRoomCreationRequest.Schemas, Is.EquivalentTo(roomCreationOptions.Schemas));
            foreach (var schema in roomCreationOptions.Schemas)
            {
                Assert.That(localRoomCreationRequest.Schemas, Does.Contain(schema));
            }

            Assert.That(localRoomCreationRequest.DisconnectTimeout, Is.Not.Zero);
            Assert.That(localRoomCreationRequest.DebugStreams, Is.EqualTo(roomCreationOptions.UseDebugStreams));
            Assert.That(localRoomCreationRequest.Frequency, Is.Zero);
            Assert.That(localRoomCreationRequest.MinQueryDistance, Is.Not.Zero);
            Assert.That(localRoomCreationRequest.WebSupport, Is.True);
            Assert.That(localRoomCreationRequest.CleanupTimeout, Is.EqualTo(roomCreationOptions.CleanupTimeout));
            Assert.That(localRoomCreationRequest.ProjectID, Is.EqualTo(roomCreationOptions.ProjectId));

            Assert.That(localRoomCreationRequest.KeyValues.Count, Is.EqualTo(roomCreationOptions.KeyValues.Count));
            foreach (var kv in roomCreationOptions.KeyValues)
            {
                Assert.That(localRoomCreationRequest.KeyValues.ContainsKey(kv.Key), Is.True);
                Assert.That(localRoomCreationRequest.KeyValues[kv.Key], Is.EqualTo(kv.Value));
            }

            Assert.That(localRoomCreationRequest.Tags, Is.EquivalentTo(roomCreationOptions.Tags));
            foreach (var tag in roomCreationOptions.Tags)
            {
                Assert.That(localRoomCreationRequest.Tags, Does.Contain(tag));
            }

            Assert.That(localRoomCreationRequest.Secret, Is.EqualTo(roomCreationOptions.Secret));
        }
    }
}
