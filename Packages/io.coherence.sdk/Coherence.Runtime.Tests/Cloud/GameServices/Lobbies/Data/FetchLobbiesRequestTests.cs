// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System.Collections.Generic;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class FetchLobbiesRequestTests : CoherenceTest
    {
        private const string USWestRegion = "us-west-1";
        private const int MaxPlayers = 8;

        [Test]
        [Description("Tests that a FetchLobbiesRequest can be serialized and deserialized correctly.")]
        public void FetchLobbiesRequest_Serialization_FullCircle()
        {
            var lobbyFilters = new List<LobbyFilter>
            {
                new LobbyFilter().WithMaxPlayers(FilterOperator.Equals, MaxPlayers),
                new LobbyFilter().WithRegion(FilterOperator.Equals, new[]
                {
                    USWestRegion
                }),
            };

            var lobbySortOptions = new List<LobbySortOption>
            {
                new()
                {
                    Key = "createdAt",
                    Descending = true,
                },
            };

            var originalRequest = new FetchLobbiesRequest
            {
                LobbyFilters = lobbyFilters,
                Limit = 25,
                PublicOnly = true,
                Sort = lobbySortOptions,
            };

            var serialized = JsonConvert.SerializeObject(originalRequest);
            var deserialized = JsonConvert.DeserializeObject<FetchLobbiesRequest>(serialized);

            Assert.That(deserialized.Limit, Is.EqualTo(originalRequest.Limit));
            Assert.That(deserialized.PublicOnly, Is.EqualTo(originalRequest.PublicOnly));
            Assert.That(deserialized.LobbyFilters.Count, Is.EqualTo(originalRequest.LobbyFilters.Count));
            for (var i = 0; i < deserialized.LobbyFilters.Count; i++)
            {
                Assert.That(deserialized.LobbyFilters[i].Key, Is.EqualTo(originalRequest.LobbyFilters[i].Key));
                Assert.That(deserialized.LobbyFilters[i].Values.Count,
                    Is.EqualTo(originalRequest.LobbyFilters[i].Values.Count));
            }

            Assert.That(deserialized.Sort.Count, Is.EqualTo(originalRequest.Sort.Count));
            for (var i = 0; i < deserialized.Sort.Count; i++)
            {
                Assert.That(deserialized.Sort[i].Key, Is.EqualTo(originalRequest.Sort[i].Key));
                Assert.That(deserialized.Sort[i].Descending, Is.EqualTo(originalRequest.Sort[i].Descending));
            }
        }

        [Test]
        [Description("Tests that GetRequestBody returns default options when null options are provided.")]
        public void GetRequestBody_Returns_DefaultOptions_When_Null_Options_Provided()
        {
            var requestBodyJson = FetchLobbiesRequest.GetRequestBody(null);
            var requestBody = JsonConvert.DeserializeObject<FetchLobbiesRequest>(requestBodyJson);

            Assert.That(requestBody.Limit, Is.EqualTo(10));
            Assert.That(requestBody.PublicOnly, Is.True);
            Assert.That(requestBody.LobbyFilters, Is.Null);
            Assert.That(requestBody.Sort, Is.Null);
        }

        [Test]
        [Description("Tests that GetRequestBody includes correct options when provided.")]
        public void GetRequestBody_Includes_Correct_Options_When_Provided()
        {
            var lobbyFilters = new List<LobbyFilter>
            {
                new LobbyFilter().WithMaxPlayers(FilterOperator.Equals, MaxPlayers),
            };

            var sortOptions = new Dictionary<SortOptions, bool>
            {
                { SortOptions.createdAt, true },
            };

            var findLobbyOptions = new FindLobbyOptions
            {
                Limit = MaxPlayers,
                LobbyFilters = lobbyFilters,
                Sort = sortOptions,
            };

            var requestBodyJson = FetchLobbiesRequest.GetRequestBody(findLobbyOptions);
            var requestBody = JsonConvert.DeserializeObject<FetchLobbiesRequest>(requestBodyJson);

            Assert.That(requestBody.Limit, Is.EqualTo(findLobbyOptions.Limit));
            Assert.That(requestBody.PublicOnly, Is.True);
            Assert.That(requestBody.LobbyFilters.Count, Is.EqualTo(findLobbyOptions.LobbyFilters.Count));
            Assert.That(requestBody.LobbyFilters[0].Key, Is.EqualTo("maxPlayers"));
            Assert.That(requestBody.LobbyFilters[0].Values[0], Is.EqualTo(MaxPlayers));
            Assert.That(requestBody.Sort.Count, Is.EqualTo(findLobbyOptions.Sort.Count));
            Assert.That(requestBody.Sort[0].Key, Is.EqualTo("createdAt"));
            Assert.That(requestBody.Sort[0].Descending, Is.True);
        }
    }
}
