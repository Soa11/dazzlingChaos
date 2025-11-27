// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using Moq;
    using Moq.Language.Flow;
    using NUnit.Framework;

    /// <summary>
    /// Edit mode unit tests for <see cref="RegionsService"/>.
    /// </summary>
    public class RegionsServiceTests : CoherenceTest
    {
        private Mock<IAuthClientInternal> authClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            authClient = new(MockBehavior.Strict);
            authClient.SetupGet(ac => ac.SessionToken).Returns(SessionToken.None);
        }

        [Test]
        public async Task FetchRegionsAsync_Should_Contain_No_Regions_When_Request_Factory_Returns_Empty_Result()
        {
            var requestMock = new Mock<IRequestFactoryInternal>();
            SetSendRequestAsync(requestMock).Returns(Task.FromResult(CoherenceJson.SerializeObject(new RegionFetchResponse
            {
                Regions = Array.Empty<string>(),
                PingServers = Array.Empty<PingServer>()
            })));

            var regionsService = new RegionsService(requestMock.Object, authClient.Object);

            var response = await regionsService.FetchRegionsAsync();

            Assert.That(response.Length, Is.Zero);
        }

        [Test]
        public async Task FetchRegionsAsync_Should_Contain_Two_Regions_When_Request_Factory_Returns_Two_Results()
        {
            var requestMock = new Mock<IRequestFactoryInternal>();
            SetSendRequestAsync(requestMock).Returns(Task.FromResult(CoherenceJson.SerializeObject(new RegionFetchResponse
            {
                Regions = new[] { "eu", "us" },
                PingServers = new PingServer[]
                {
                    new() { Region = "eu", Port = 29165, Ip = "18.196.114.164" },
                    new() { Region = "us", Port = 29166, Ip = "18.196.114.165" }
                }
            })));

            var regionsService = new RegionsService(requestMock.Object, authClient.Object);

            var response = await regionsService.FetchRegionsAsync();

            Assert.That(response.Length, Is.EqualTo(2));
        }

        [Test]
        public async Task SendRequestAsyncReturns_Should_Fail_When_Request_Factory_Throws_Exception()
        {
            var requestMock = new Mock<IRequestFactoryInternal>();
            SetSendRequestAsync(requestMock).Throws(new RequestException(ErrorCode.Unknown));

            var regionsService = new RegionsService(requestMock.Object, authClient.Object);

            try
            {
                await regionsService.FetchRegionsAsync();
            }
            catch (Exception)
            {
                return;
            }

            Assert.Fail("Expected exception was not thrown.");
        }

        [Test]
        public async Task SendRequestAsyncReturns_Should_Be_Canceled_When_Cancellation_Is_Requested()
        {
            var requestMock = new Mock<IRequestFactoryInternal>();
            SetSendRequestAsync(requestMock).Returns(Task.FromResult(CoherenceJson.SerializeObject(new RegionFetchResponse
            {
                Regions = Array.Empty<string>(),
                PingServers = Array.Empty<PingServer>()
            })));

            var regionsService = new RegionsService(requestMock.Object, authClient.Object);

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            try
            {
                await regionsService.FetchRegionsAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            Assert.Fail("Expected TaskCanceledException was not thrown.");
        }

        [Test]
        public async Task SendRequestAsyncReturns_Should_Wait_Until_Ongoing_Requests_Complete()
        {
            var firstRequestCompletionSource = new TaskCompletionSource<string>();

            var mockRequestFactory = new MockRequestFactoryBuilder()
                .SendRequestAsyncReturns
                (
                    // first call - controlled by TaskCompletionSource
                    () => firstRequestCompletionSource.Task,
                    // second call
                    () => Task.FromResult(CoherenceJson.SerializeObject(new RegionFetchResponse
                    {
                        Regions = Array.Empty<string>(),
                        PingServers = Array.Empty<PingServer>()
                    }))
                )
                .Build();

            var regionsService = new RegionsService(mockRequestFactory, authClient.Object);

            var fetchRegionsTask1 = regionsService.FetchRegionsAsync();
            var fetchRegionsTask2 = regionsService.FetchRegionsAsync();

            await Task.Yield();

            Assert.That(fetchRegionsTask2.IsCompleted, Is.False);

            // Complete the first request
            firstRequestCompletionSource.SetResult(CoherenceJson.SerializeObject(new RegionFetchResponse
            {
                Regions = Array.Empty<string>(),
                PingServers = Array.Empty<PingServer>()
            }));

            await Task.WhenAll(fetchRegionsTask1, fetchRegionsTask2);

            Assert.That(fetchRegionsTask2.IsCompletedSuccessfully, Is.True);
        }

        private static ISetup<IRequestFactoryInternal, Task<string>> SetSendRequestAsync(Mock<IRequestFactoryInternal> requestMock) => requestMock.Setup(factory => factory.SendRequestAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string>(),
            It.IsAny<string>()));
    }
}
