// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Transport
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using Brook;
    using Brook.Octet;
    using Common;
    using Common.Pooling;
    using Connection;
    using Flux;
    using Log;
    using Stats;

    /// <summary>
    /// In-memory transport for direct connection with the RS. Uses ConcurrentQueues instead of sockets.
    /// Can also be used as a drop-in replacement for UdpTransportV2 for testing purposes.
    /// </summary>
    public class MemoryTransport : IListenTransport
    {
        private struct TransportEvent
        {
            public IOutOctetStream Stream;
            public IPEndPoint From;
            public ConnectionException Error;
        }

        public const int HeaderSizeBytes = Flux.roomByteCount + SessionID.Size;

        public event Action OnOpen;
        public event Action<ConnectionException> OnError;

        public TransportState State { get; private set; }
        public bool IsReliable => true;
        public bool CanSend => State == TransportState.Open;
        public int HeaderSize => HeaderSizeBytes;
        public string Description => "Memory";

        private const int DefaultStreamCount = 64;

        private bool IsInListenMode => remoteEndPoint == null;
        private bool IsInClientMode => !IsInListenMode;

        private IPEndPoint remoteEndPoint;
        private IPEndPoint localEndPoint;
        private readonly IStats stats;
        private readonly Logger logger;

        private SessionID sessionId;
        private ushort roomId;
        private ushort maxBufferSize;

        private readonly Pool<PooledInOctetStream> streamPool;

        // Shared queues for communication between transports.  This is a static dictionary of all active
        // enpoints of transports so listeners can pick up connections directed at them.
        private static readonly Func<IPEndPoint, ConcurrentQueue<TransportEvent>> QueueFactory = _ => new();
        private static readonly ConcurrentDictionary<IPEndPoint, ConcurrentQueue<TransportEvent>> endpointQueues = new();

        public MemoryTransport(ushort maxBufferSize, IStats stats, Logger logger)
        {
            this.stats = stats;
            this.logger = logger.With<MemoryTransport>();
            this.maxBufferSize = maxBufferSize;

            streamPool = Pool<PooledInOctetStream>
                .Builder(pool => new(pool, maxBufferSize))
                .Prefill(DefaultStreamCount)
                .Build();
        }

        public void Open(EndpointData endpoint, ConnectionSettings settings)
        {
            logger.Debug("Open",
                ("endpoint", endpoint),
                ("maxBufferSize", maxBufferSize));

            try
            {
                remoteEndPoint = GetIPEndPoint(endpoint);
                roomId = endpoint.roomId;

                // Generate a unique local endpoint for this client
                localEndPoint = new IPEndPoint(IPAddress.Loopback, GenerateRandomPort());

                // Ensure queues exist
                _ = GetOrCreateQueue(localEndPoint);
                _ = GetOrCreateQueue(remoteEndPoint);
            }
            catch (Exception exception)
            {
                OnError?.Invoke(new ConnectionException("Open failed", exception));
                return;
            }

            State = TransportState.Open;
            OnOpen?.Invoke();
        }

        public void Listen(EndpointData endpoint, ConnectionSettings settings)
        {
            logger.Debug("Listen",
                ("endpoint", endpoint),
                ("maxBufferSize", maxBufferSize));

            try
            {
                roomId = endpoint.roomId;
                localEndPoint = GetIPEndPoint(endpoint);

                // Ensure the queue exists for this endpoint
                _ = GetOrCreateQueue(localEndPoint);
            }
            catch (Exception exception)
            {
                OnError?.Invoke(new ConnectionException("Listen failed", exception));
                return;
            }

            State = TransportState.Open;
            OnOpen?.Invoke();
        }

        public void Close()
        {
            Close(remoteEndPoint);
        }

        public void Close(IPEndPoint endPoint)
        {
            logger.Debug("Close");
            State = TransportState.Closed;

            // send null data to close the connection.
            if (endPoint != null && localEndPoint != null)
            {
                //var transportEvent = eventPool.Rent();
                var transportEvent = new TransportEvent();
                transportEvent.Stream = null;
                transportEvent.From = localEndPoint;
                transportEvent.Error = null;

                var targetQueue = GetOrCreateQueue(endPoint);
                targetQueue.Enqueue(transportEvent);
            }

            // Clean up the queue for this endpoint
            if (localEndPoint != null)
            {
                if (endpointQueues.TryRemove(localEndPoint, out var queue))
                {
                    foreach (var entry in queue)
                    {
                        var stream = entry.Stream;
                        stream.ReturnIfPoolable();
                    }
                }
            }
        }

        public void Send(IOutOctetStream stream)
            => Send(stream, sessionId);

        public void SendTo(IOutOctetStream stream, IPEndPoint endpoint, SessionID sessionID)
            => Send(stream, sessionID, endpoint);

        private void Send(IOutOctetStream stream, SessionID sessionID, IPEndPoint endpoint = null)
        {
            var targetEndpoint = endpoint ?? remoteEndPoint;

            logger.Trace("Send", ("mode", IsInClientMode ? "Client" : "Listen"),
                ("sessionID", sessionID), ("to", targetEndpoint),
                ("data", stream.Position));

            WriteHeader(stream, sessionID);

            // Send to the target endpoint's queue
            //var transportEvent = eventPool.Rent();
            var transportEvent = new TransportEvent();
            transportEvent.Stream = stream;
            transportEvent.From = localEndPoint;
            transportEvent.Error = null;

            var targetQueue = GetOrCreateQueue(targetEndpoint);
            targetQueue.Enqueue(transportEvent);
        }

        private void WriteHeader(IOutOctetStream stream, SessionID sessionID)
        {
            var streamEnd = stream.Position;
            stream.Seek(0);

            stream.WriteUint16(roomId);
            SessionID.Write(sessionID, stream);

            stream.Seek(streamEnd);
        }

        public void Receive(List<(IInOctetStream, IPEndPoint)> buffer)
        {
            if (State == TransportState.Closed)
            {
                logger.Debug("DBG_ERROR: Receive in the closed state");
                return;
            }

            if (localEndPoint == null)
            {
                return;
            }

            var queue = GetOrCreateQueue(localEndPoint);

            while (queue.TryDequeue(out var transportEvent))
            {
                var receivedStream = transportEvent.Stream;
                var from = transportEvent.From;

                if (!IsInListenMode && !remoteEndPoint.Equals(from))
                {
                    receivedStream?.ReturnIfPoolable();

                    logger.Warning(
                        Warning.TransportReceivedFromInvalidIP,
                        ("expected", remoteEndPoint),
                        ("actual", from));
                }

                if (receivedStream == null)
                {
                    OnError?.Invoke(new ConnectionClosedException(from, "peer closed", default));

                    return;
                }

                logger.Trace("DataReceived", ("mode", IsInClientMode ? "Client" : "Listen"),
                    ("from", from),
                    ("data", receivedStream.Position));

                var stream = streamPool.Rent();
                stream.Reset(transportEvent.Stream.Octets);
                receivedStream.ReturnIfPoolable();

                try
                {
                    if (!HandleRoomId(stream))
                    {
                        stream.Return();

                        continue;
                    }

                    // In Listen mode it is the responsibility of the caller to handle the sessionID
                    if (IsInClientMode && !HandleSessionId(stream))
                    {
                        stream.Return();

                        continue;
                    }
                }
                catch (Exception exception)
                {
                    stream.Return();
                    OnError?.Invoke(new ConnectionException("Failed to read roomID/sessionID", exception));

                    return;
                }

                buffer.Add((stream, transportEvent.From));
            }
        }

        private bool HandleRoomId(InOctetStream stream)
        {
            if (stream.RemainingOctetCount < Flux.roomByteCount)
            {
                return false;
            }

            var packetRoomId = stream.ReadUint16();
            if (packetRoomId == this.roomId)
            {
                return true;
            }

            logger.Warning(
                Warning.TransportWrongRoomID,
                ("expected", this.roomId),
                ("actual", packetRoomId));
            return false;
        }

        private bool HandleSessionId(InOctetStream stream)
        {
            if (stream.RemainingOctetCount < SessionID.Size)
            {
                return false;
            }

            var packetSessionId = SessionID.Read(stream);
            if (sessionId == SessionID.None)
            {
                sessionId = packetSessionId;
                logger.Debug("SessionID set", ("sessionID", sessionId));
                return true;
            }

            if (sessionId == packetSessionId)
            {
                return true;
            }

            logger.Debug(
                "Packet with wrong sessionID",
                ("expected", sessionId),
                ("actual", packetSessionId));
            return false;
        }

        private static IPEndPoint GetIPEndPoint(in EndpointData endpoint)
        {
            return new IPEndPoint(IPAddress.Parse(endpoint.host), endpoint.port);
        }

        private ConcurrentQueue<TransportEvent> GetOrCreateQueue(IPEndPoint endpoint)
        {
            return endpointQueues.GetOrAdd(endpoint, QueueFactory);
        }

        private static int GenerateRandomPort()
        {
            // Generate a random port in the dynamic/private range (49152-65535)
            return new Random().Next(49152, 65536);
        }

        /// <summary>
        /// Clears all endpoint queues. Useful for test cleanup.
        /// </summary>
        public static void ClearAllQueues()
        {
            foreach (var queues in endpointQueues)
            {
                var queue = queues.Value;
                foreach (var transportEvent in queue)
                {
                    var stream = transportEvent.Stream;
                    stream.ReturnIfPoolable();
                }

                queue.Clear();
            }

            endpointQueues.Clear();
        }
    }
}
