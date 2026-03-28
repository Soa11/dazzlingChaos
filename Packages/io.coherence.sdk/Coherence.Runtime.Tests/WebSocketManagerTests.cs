// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Threading.Tasks;
    using Coherence.Tests;
    using Coherence.Utils;
    using Common;
    using Connection;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Edit mode unit tests for <see cref="WebSocketManager"/>.
    /// </summary>
    public class WebSocketManagerTests : CoherenceTest
    {
        private Mock<IRuntimeSettings> runtimeSettingsMock;
        private RequestIdSource idSource;
        private WebSocketManager webSocketManager;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            runtimeSettingsMock = new();
            runtimeSettingsMock.Setup(x => x.WebSocketEndpoint).Returns("WebSocketEndpoint");
            runtimeSettingsMock.Setup(x => x.RuntimeKey).Returns("RuntimeKey");
            runtimeSettingsMock.Setup(x => x.SdkVersion).Returns("SdkVersion");
            idSource = new();
            webSocketManager = new(runtimeSettingsMock.Object, idSource);
        }

        [TearDown]
        public override void TearDown()
        {
            webSocketManager?.Dispose();
            base.TearDown();
        }

        [Test]
        public void AddPushCallback_Returns_False_If_Callback_For_RequestId_Already_Registered()
        {
            webSocketManager.AddPushCallback("/test/path", null);

            var wasAdded = webSocketManager.AddPushCallback("/test/path", null);

            Assert.That(wasAdded, Is.False, "Should not allow duplicate callback registration.");
        }

        [Test]
        public void WebSocketManager_Constructor_Registers_Callback_For_ConnectionReconnectPath()
        {
            var wasCallbackMissing = webSocketManager.AddPushCallback(WebSocketManager.ConnectionReconnectPath, null);

            Assert.That(wasCallbackMissing, Is.False, $"WebSocketManager constructor should already have registered callback for \"{WebSocketManager.ConnectionReconnectPath}\"");
        }

        [Test]
        public async Task WebSocketManager_Disconnects_And_Reconnects_When_It_Receives_Reconnect_Push_Message()
        {
            var responseMeta = CoherenceJson.SerializeObject(new ResponseMeta { RequestId = WebSocketManager.ConnectionReconnectPath });
            var text = responseMeta + "\nBody";
            webSocketManager.Connect();
            Assert.That(webSocketManager.Enabled, Is.True);

            webSocketManager.HandleResponse(text);

            // WebSocket should be disabled immediately after reconnect is requested.
            Assert.That(webSocketManager.Enabled, Is.False);

            var waitUntil = DateTime.Now.AddMilliseconds(WebSocketManager.MaxWaitBeforeReconnectMilliSeconds);
            while (DateTime.Now < waitUntil)
            {
                await Task.Yield();
            }
            await Task.Yield();

            // WebSocket should be re-enabled after MaxWaitBeforeReconnectMilliSeconds milliseconds (at the latest).
            Assert.That(webSocketManager.Enabled, Is.True);
        }
    }
}
