// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using Coherence.Brook;
    using Coherence.Common;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.SimulationFrame;
    using Coherence.Stats;

    /// <summary>
    /// InNetworkChannel is responsible for deserializing and buffering incoming changes.
    /// While taking care of entity references and ordering.
    /// </summary>
    internal class InNetworkChannel : IInNetworkChannel
    {
        public event Action<List<IncomingEntityUpdate>> OnEntityUpdate;
        public event Action<IEntityCommand> OnCommand;
        public event Action<IEntityInput> OnInput;

        private readonly ISchemaSpecificComponentDeserialize deserializer;
        private readonly IComponentInfo definition;
        private readonly IMessageDeserializer messageDeserializer;
        private readonly ChannelID channelID;

        private readonly ReceiveChangeBuffer changeBuffer;

        private readonly Stats stats;
        private readonly Logger logger;

        // Cache for HandleEntityUpdate() and FlushBuffer() so we don't need to re-allocate the list every time
        private readonly List<IncomingEntityUpdate> updatesBuffer = new(32);

        // Cache for FlushBuffer() so we don't need to re-allocate the list every time
        private readonly List<IEntityMessage> messagesBuffer = new(32);

        public InNetworkChannel(
            ISchemaSpecificComponentDeserialize deserializer,
            IComponentInfo definition,
            IEntityRegistry entityRegistry,
            ChannelID channelID,
            Stats stats,
            Logger logger)
        {
            this.deserializer = deserializer;
            this.definition = definition;
            this.stats = stats;
            this.logger = logger.With<InNetworkChannel>();
            this.messageDeserializer = new MessageDeserializer(deserializer, this.logger);
            this.channelID = channelID;

            this.changeBuffer = new ReceiveChangeBuffer(entityRegistry, this.logger);
        }

        public bool Deserialize(IInBitStream stream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            var gotEntityUpdate = false;

            while (DeserializeCommands.DeserializeCommand(stream, out var messageType))
            {
                if (messageType == MessageType.EcsWorldUpdate)
                {
                    gotEntityUpdate = true;
                }

                PerformMessage(messageType, packetSimulationFrame, stream, packetFloatingOrigin);
            }

            return gotEntityUpdate;
        }

        public List<RefsInfo> GetRefsInfos()
        {
            return changeBuffer.GetRefsInfos();
        }

        public bool FlushBuffer(IReadOnlyCollection<Entity> resolvableEntities)
        {
            var didFlushAnything = false;

            updatesBuffer.Clear();
            changeBuffer.TakeUpdates(updatesBuffer, resolvableEntities);
            OnEntityUpdate?.Invoke(updatesBuffer);
            didFlushAnything |= updatesBuffer.Count > 0;

            messagesBuffer.Clear();
            changeBuffer.TakeCommands(messagesBuffer);
            foreach (var message in messagesBuffer)
            {
                OnCommand?.Invoke((IEntityCommand)message);
            }
            didFlushAnything |= messagesBuffer.Count > 0;

            messagesBuffer.Clear();
            changeBuffer.TakeInputs(messagesBuffer);
            foreach (var entityMessage in messagesBuffer)
            {
                OnInput?.Invoke((IEntityInput)entityMessage);
            }
            didFlushAnything |= messagesBuffer.Count > 0;

            return didFlushAnything;
        }

        public void Clear()
        {
            changeBuffer.Clear();
        }

        private void PerformMessage(MessageType messageType, AbsoluteSimulationFrame packetSimulationFrame, IInBitStream bitStream, Vector3d packetFloatingOrigin)
        {
            switch (messageType)
            {
                case MessageType.EcsWorldUpdate:
                    HandleEntityUpdate(packetSimulationFrame, bitStream, packetFloatingOrigin);
                    break;
                case MessageType.Command:
                    HandleCommands(bitStream);
                    break;
                case MessageType.Input:
                    HandleInputs(bitStream);
                    break;
                default:
                    logger.Warning(Warning.CoreInNetworkChannelUnknownMessage,
                         ("MessageCode", messageType),
                         ("SimFrame", packetSimulationFrame.Frame),
                         ("RemainingBits", bitStream.RemainingBits()));
                    break;
            }
        }

        private void HandleCommands(IInBitStream bitStream)
        {
            var commandsData = messageDeserializer.ReadCommands(bitStream);
            var numCommands = commandsData.Length;
            stats.TrackIncomingMessages(MessageType.Command, numCommands);

            for (var i = 0; i < numCommands; i++)
            {
                changeBuffer.AddCommand(commandsData[i]);
            }
        }

        private void HandleInputs(IInBitStream bitStream)
        {
            var inputData = messageDeserializer.ReadInputs(bitStream);
            var numInputs = inputData.Length;
            stats.TrackIncomingMessages(MessageType.Input, numInputs);

            for (var i = 0; i < numInputs; i++)
            {
                changeBuffer.AddInput(inputData[i]);
            }
        }

        private void HandleEntityUpdate(AbsoluteSimulationFrame packetSimulationFrame, IInBitStream bitStream,
            Vector3d floatingOrigin)
        {
            updatesBuffer.Clear();
            Serializer.Deserialize.ReadWorldUpdate(updatesBuffer, packetSimulationFrame, floatingOrigin, deserializer, bitStream,
                definition, channelID, logger);
            stats.TrackIncomingMessages(MessageType.EcsWorldUpdate, updatesBuffer.Count);

            foreach (var update in updatesBuffer)
            {
                changeBuffer.AddChange(update);
            }
        }
    }
}
