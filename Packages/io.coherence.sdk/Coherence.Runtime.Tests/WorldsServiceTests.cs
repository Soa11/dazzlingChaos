// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Common;
    using Moq;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Moq.Language.Flow;

    /// <summary>
    /// Edit mode unit tests for <see cref="WorldsService"/>.
    /// </summary>
    public class WorldsServiceTests : CoherenceTest
    {
        private const string TwoWorldsResponse = "[{\"id\":1173779553,\"project_id\":\"cbkehjjri4dbib3tu900\",\"name\":\"World1_EU\",\"schema_id\":\"826a89bad874101820a571e515e5b068292a7877\",\"ccu_target\":20,\"rs_size\":\"\",\"rs_version\":\"v0.43.0\",\"sdk_version\":\"0.10.9\",\"sim_size\":\"\",\"sim_slug\":\"\",\"sim_args\":\"\",\"rs_args\":\"\",\"rs_send_frequency\":20,\"rs_recv_frequency\":60,\"tags\":[],\"region\":\"eu\",\"created_at\":\"2023-03-02T06:34:25.384866Z\",\"updated_at\":\"2023-03-02T06:34:25.384866Z\",\"rs_resource\":{\"enabled\":false,\"cpu\":0,\"memory\":0,\"tier\":\"\",\"hard_memory\":0},\"sim_resource\":{\"enabled\":false,\"cpu\":0,\"memory\":0,\"tier\":\"\",\"hard_memory\":0},\"schema\":{\"id\":\"\",\"hashes\":null,\"project_id\":\"\",\"sdk_version\":\"\",\"commit\":\"\",\"timestamp\":\"0001-01-01T00:00:00Z\"},\"host\":{\"rsid\":\"ba4e5854-4712-fe53-838a-4c2e3797e78d\",\"ip\":\"18.196.114.164\",\"udp_port\":29165,\"web_port\":22475,\"sig_port\":31203,\"region\":\"eu\",\"job_id\":\"rsw-eu-cbkehjjri4dbib3tu900-1173779553\",\"max_ccu\":0},\"rs_status\":{\"exists\":false,\"running\":false,\"healthy\":false,\"error\":\"\",\"desired_status\":\"\"},\"pc_status\":{\"exists\":false,\"running\":false,\"healthy\":false,\"error\":\"\",\"desired_status\":\"\"},\"sim_status\":{\"exists\":false,\"running\":false,\"healthy\":false,\"error\":\"\",\"desired_status\":\"\"}},{\"id\":1307510269,\"project_id\":\"cbkehjjri4dbib3tu900\",\"name\":\"World2_US\",\"schema_id\":\"826a89bad874101820a571e515e5b068292a7877\",\"ccu_target\":20,\"rs_size\":\"\",\"rs_version\":\"v0.43.0\",\"sdk_version\":\"0.10.9\",\"sim_size\":\"\",\"sim_slug\":\"\",\"sim_args\":\"\",\"rs_args\":\"\",\"rs_send_frequency\":20,\"rs_recv_frequency\":60,\"tags\":[],\"region\":\"us\",\"created_at\":\"2023-03-02T06:34:44.57957Z\",\"updated_at\":\"2023-03-02T06:34:44.57957Z\",\"rs_resource\":{\"enabled\":false,\"cpu\":0,\"memory\":0,\"tier\":\"\",\"hard_memory\":0},\"sim_resource\":{\"enabled\":false,\"cpu\":0,\"memory\":0,\"tier\":\"\",\"hard_memory\":0},\"schema\":{\"id\":\"\",\"hashes\":null,\"project_id\":\"\",\"sdk_version\":\"\",\"commit\":\"\",\"timestamp\":\"0001-01-01T00:00:00Z\"},\"host\":{\"rsid\":\"1142c968-f2be-4dc4-9955-f4894ea9aa5d\",\"ip\":\"44.204.106.106\",\"udp_port\":21627,\"web_port\":20872,\"sig_port\":20112,\"region\":\"us\",\"job_id\":\"rsw-us-cbkehjjri4dbib3tu900-1307510269\",\"max_ccu\":0},\"rs_status\":{\"exists\":false,\"running\":false,\"healthy\":false,\"error\":\"\",\"desired_status\":\"\"},\"pc_status\":{\"exists\":false,\"running\":false,\"healthy\":false,\"error\":\"\",\"desired_status\":\"\"},\"sim_status\":{\"exists\":false,\"running\":false,\"healthy\":false,\"error\":\"\",\"desired_status\":\"\"}}]";
        private readonly Mock<IAuthClientInternal> authClient = new();
        private readonly Mock<IRuntimeSettings> runtimeSettings = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            runtimeSettings.Setup(settings => settings.SchemaID).Returns(string.Empty);
            runtimeSettings.Setup(settings => settings.RsVersion).Returns(string.Empty);
        }

        [Test]
        public async Task FetchWorlds_Should_Return_Empty_List_When_No_Worlds_Exist()
        {
            await using var worldsService = CreateWorldsService(sendRequestResult: "");
            var taskCompletionSource = new TaskCompletionSource<bool>();

            worldsService.FetchWorlds(OnRequestFinished);

            await taskCompletionSource.Task;

            void OnRequestFinished(RequestResponse<IReadOnlyList<WorldData>> response)
            {
                Assert.That(response.Result.Count, Is.Zero);
                Assert.That(response.Status, Is.EqualTo(RequestStatus.Success));
                taskCompletionSource.SetResult(true);
            }
        }

        [Test]
        public async Task FetchWorldsAsync_Should_Return_Empty_List_When_No_Worlds_Exist()
        {
            await using var worldsService = CreateWorldsService(sendRequestResult: "");

            var list = await worldsService.FetchWorldsAsync();

            Assert.That(list.Count, Is.Zero);
        }

        [Test]
        public async Task FetchWorlds_Should_Return_Two_Worlds_When_Two_Worlds_Exist()
        {
            await using var worldsService = CreateWorldsService(TwoWorldsResponse);
            var taskCompletionSource = new TaskCompletionSource<bool>();

            worldsService.FetchWorlds(OnRequestFinished);

            await taskCompletionSource.Task;

            void OnRequestFinished(RequestResponse<IReadOnlyList<WorldData>> response)
            {
                Assert.That(response.Result.Count, Is.EqualTo(2));
                taskCompletionSource.SetResult(true);
            }
        }

        [Test]
        public async Task FetchWorldsAsync_Should_Return_Two_Worlds_When_Two_Worlds_Exist()
        {
            await using var worldsService = CreateWorldsService(TwoWorldsResponse);

            var list = await worldsService.FetchWorldsAsync();

            Assert.That(list.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task FetchWorlds_RequestResponse_Should_Contain_Exception_Thrown_By_RequestFactory()
        {
            var sendRequestThrows = new RequestException(ErrorCode.Unknown);
            await using var worldsService = CreateWorldsService(sendRequestThrows: sendRequestThrows);
            var taskCompletionSource = new TaskCompletionSource<bool>();

            worldsService.FetchWorlds(OnRequestFinished);

            await taskCompletionSource.Task;

            void OnRequestFinished(RequestResponse<IReadOnlyList<WorldData>> response)
            {
                Assert.That(response.Exception, Is.EqualTo(sendRequestThrows));
                Assert.That(response.Status, Is.EqualTo(RequestStatus.Fail));
                taskCompletionSource.SetResult(true);
            }
        }

        [Test]
        public async Task FetchWorldsAsync_Should_Fail_If_RequestFactory_Fails()
        {
            var requestFactoryException = new RequestException(ErrorCode.Unknown);
            await using var worldsService = CreateWorldsService(sendRequestThrows: requestFactoryException);

            try
            {
                await worldsService.FetchWorldsAsync();
            }
            catch (RequestException caughtException)
            {
                Assert.That(caughtException, Is.EqualTo(requestFactoryException));
                return;
            }

            Assert.Fail("Expected RequestException was not thrown.");
        }

        [Test]
        public async Task Should_QueueCallbacks_When_RequestIsOnGoingAsync()
        {
            var mockRequestFactory = new MockRequestFactoryBuilder()
                .SendRequestAsyncReturns
                (
                    // first call
                    async () =>
                    {
                        await Task.Yield();
                        return TwoWorldsResponse;
                    },
                    // second call
                    () => Task.FromResult(TwoWorldsResponse)
                )
                .Build();

            var credentials = new CloudCredentialsPair(authClient.Object, mockRequestFactory);
            using var worldsService = new WorldsService(credentials, runtimeSettings.Object);

            var fetchWorldsTask1 = worldsService.FetchWorldsAsync();
            Assert.That(fetchWorldsTask1.IsCompletedSuccessfully, Is.False);

            var fetchWorldsTask2 = worldsService.FetchWorldsAsync();
            Assert.That(fetchWorldsTask2.IsCompleted, Is.False);

            await Task.WhenAll(fetchWorldsTask1, fetchWorldsTask2);

            Assert.That(fetchWorldsTask1.IsCompletedSuccessfully, Is.True);
            Assert.That(fetchWorldsTask2.IsCompletedSuccessfully, Is.True);
        }

        private WorldsService CreateWorldsService(string sendRequestResult) => CreateWorldsService( setup => setup.ReturnsAsync(sendRequestResult));
        private WorldsService CreateWorldsService(Exception sendRequestThrows) => CreateWorldsService( setup => setup.Throws(sendRequestThrows));
        private WorldsService CreateWorldsService(Action<ISetup<IRequestFactoryInternal, Task<string>>> setupSendRequest)
        {
            var requestMock = new Mock<IRequestFactoryInternal>();

            setupSendRequest(requestMock.Setup(factory => factory.SendRequestAsync
            (
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )));

            var credentials = new CloudCredentialsPair(authClient.Object, requestMock.Object);
            return new(credentials, runtimeSettings.Object);
        }
    }
}
