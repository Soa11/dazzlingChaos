// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_WEBGL && !UNITY_EDITOR
#define UNITY_WEBGL_BUILD
#endif

namespace Coherence.PingClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;
    using Cloud;
    using Log;

    /// <summary>
    /// Static ping client for measuring latency to coherence ping servers.
    /// Implements the coherence ping protocol: TCP and UDP echo with 1-byte payload.
    /// </summary>
    public static class PingClient
    {
        private static readonly LazyLogger logger = new(() => Log.GetLogger(typeof(PingClient)));

        private const int PingTimeoutMs = 3000;
        private const byte PingByte = 0x42; // Arbitrary byte to send
        private const int PingCount = 3; // Number of pings per connection

        private const string ErrorCancelled = "Cancelled";
        private const string ErrorResponseTimeout = "Response timeout";
        private const string ErrorInvalidResponse = "Invalid response";

        /// <summary>
        /// Asynchronously pings a list of servers using the specified protocol and returns latency measurements.
        /// </summary>
        /// <param name="regions">List of regions to ping. Each region will be pinged independently. Note that not all regions may have a ping server associated with them.</param>
        /// <param name="protocol">Network protocol to use. Default is UDP.</param>
        /// <param name="timeoutMs">Maximum time to wait for each ping response in milliseconds. Default is 3000ms.</param>
        /// <param name="cancellationToken">Optional token to cancel the ping operation.</param>
        /// <returns>
        /// A read-only list with zero or more <see cref="PingResult"/> objects, containing latency measurements or error information for each region that has a <see cref="Region.PingServer"/>.
        /// </returns>
        public static async Task<IReadOnlyList<PingResult>> PingAsync(
            Region[] regions,
            PingProtocol protocol = PingProtocol.UDP,
            int timeoutMs = PingTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            if (regions == null)
            {
                throw new ArgumentNullException(nameof(regions));
            }

            var servers = new List<PingServer>(regions.Length);
            foreach (var r in regions)
            {
                if (r.PingServer.HasValue)
                {
                    servers.Add(r.PingServer.Value);
                }
            }

            return await PingAsync(servers, protocol, timeoutMs, cancellationToken);
        }

        /// <inheritdoc cref="PingAsync(System.Collections.Generic.IReadOnlyList{Cloud.PingServer},Coherence.PingClient.PingProtocol,int,System.Threading.CancellationToken)"/>
        public static async Task<IReadOnlyList<PingResult>> PingAsync(
            PingServer[] servers,
            PingProtocol protocol = PingProtocol.UDP,
            int timeoutMs = PingTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            return await PingAsync((IReadOnlyList<PingServer>)servers, protocol, timeoutMs, cancellationToken);
        }

        /// <summary>
        /// Asynchronously pings a list of servers using the specified protocol and returns latency measurements.
        /// </summary>
        /// <param name="servers">List of servers to ping. Each server will be pinged independently.</param>
        /// <param name="protocol">Network protocol to use. Default is UDP.</param>
        /// <param name="timeoutMs">Maximum time to wait for each ping response in milliseconds. Default is 3000ms.</param>
        /// <param name="cancellationToken">Optional token to cancel the ping operation.</param>
        /// <returns>A read-only list of PingResult objects containing latency measurements or error information for each server.</returns>
        public static async Task<IReadOnlyList<PingResult>> PingAsync(
            IReadOnlyList<PingServer> servers,
            PingProtocol protocol = PingProtocol.UDP,
            int timeoutMs = PingTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            ThrowIfUnsupportedPlatform();

            if (servers == null)
            {
                throw new ArgumentNullException(nameof(servers));
            }

            if (servers.Count == 0)
            {
                return Array.Empty<PingResult>();
            }

            logger.Debug($"Pinging {protocol}",
                ("servers", $"[{string.Join(", ", servers.Select(ep => ep.ToString()))}]"));

            if (cancellationToken.IsCancellationRequested)
            {
                return CreateCancelledResults(servers, protocol);
            }

            return await Task.Run(async () =>
            {
                var tasks = new List<Task<PingResult>>();

                foreach (var server in servers)
                {
                    tasks.Add(PingServer(server, protocol, timeoutMs, cancellationToken));
                }

                var results = await Task.WhenAll(tasks);

                logger.Debug($"{protocol} pinging finished",
                    ("results", $"[{string.Join(", ", results.Select(res => res.ToString()))}]"));

                return results;
            });
        }

        /// <summary>
        /// Asynchronously pings a collection of servers using the specified protocol and returns latency measurements.
        /// </summary>
        /// <param name="servers">Collection of servers to ping. Each server will be pinged independently.</param>
        /// <param name="protocol">Network protocol to use. Default is UDP.</param>
        /// <param name="timeoutMs">Maximum time to wait for each ping response in milliseconds. Default is 3000ms.</param>
        /// <param name="cancellationToken">Optional token to cancel the ping operation.</param>
        /// <returns>A read-only list of PingResult objects containing latency measurements or error information for each server.</returns>
        public static async Task<IReadOnlyList<PingResult>> PingAsync(
            IEnumerable<PingServer> servers,
            PingProtocol protocol = PingProtocol.UDP,
            int timeoutMs = PingTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            ThrowIfUnsupportedPlatform();

            if (servers is null)
            {
                throw new ArgumentNullException(nameof(servers));
            }

            var serversList = servers as IReadOnlyList<PingServer> ?? servers.ToArray();
            return await PingAsync(serversList, protocol, timeoutMs, cancellationToken);
        }

        /// <inheritdoc cref="Ping(System.Collections.Generic.IReadOnlyList{Cloud.PingServer},System.Action{System.Collections.Generic.IReadOnlyList{Coherence.PingClient.PingResult}},Coherence.PingClient.PingProtocol,int,System.Threading.CancellationToken)"/>
        public static void Ping(
            PingServer[] servers,
            Action<IReadOnlyList<PingResult>> onPingFinished,
            PingProtocol protocol = PingProtocol.UDP,
            int timeoutMs = PingTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            Ping((IReadOnlyList<PingServer>)servers, onPingFinished, protocol, timeoutMs, cancellationToken);
        }

        /// <summary>
        /// Pings a list of servers using the specified protocol and invokes a callback with the results.
        /// </summary>
        /// <param name="servers">List of servers to ping. Each server will be pinged independently.</param>
        /// <param name="onPingFinished">Callback action invoked when all ping operations complete. Receives a list of results.</param>
        /// <param name="protocol">Network protocol to use. Default is UDP.</param>
        /// <param name="timeoutMs">Maximum time to wait for each ping response in milliseconds. Default is 3000ms.</param>
        /// <param name="cancellationToken">Optional token to cancel the ping operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when onPingFinished is null.</exception>
        public static void Ping(
            IReadOnlyList<PingServer> servers,
            Action<IReadOnlyList<PingResult>> onPingFinished,
            PingProtocol protocol = PingProtocol.UDP,
            int timeoutMs = PingTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            ThrowIfUnsupportedPlatform();

            if (onPingFinished == null)
            {
                throw new ArgumentNullException(nameof(onPingFinished));
            }

            PingAsync(servers, protocol, timeoutMs,
                cancellationToken).ContinueWith(task =>
            {
                try
                {
                    onPingFinished(task.Result);
                }
                catch (Exception e)
                {
                    CreateErrorResults(servers, protocol, e.Message);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private static async Task<PingResult> PingServer(PingServer server, PingProtocol protocol, int timeoutMs, CancellationToken cancellationToken)
        {
            var result = new PingResult
            {
                Region = server.Region,
                Protocol = protocol
            };

            if (!ValidateServer(server, ref result))
            {
                return result;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                result.Error = ErrorCancelled;
                return result;
            }

            try
            {
                // Combine timeout and cancellation token
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    new CancellationTokenSource(timeoutMs).Token);

                using var client = GetSocket(protocol);
                client.ReceiveTimeout = timeoutMs;
                client.SendTimeout = timeoutMs;

                var buffer = new[] { PingByte };
                var totalPingMs = 0;
                var successfulPingCount = 0;

                if (protocol == PingProtocol.UDP)
                {
                    client.Connect(server.Ip, server.Port);
                }
                else
                {
                    var connectTask = client.ConnectAsync(server.Ip, server.Port);
                    var finished = await RunWithCancellation(connectTask, cts.Token);
                    if (!finished)
                    {
                        result.Error = cancellationToken.IsCancellationRequested ? ErrorCancelled : ErrorResponseTimeout;
                        return result;
                    }

                    await connectTask;
                }

                for (var i = 0; i < PingCount; i++)
                {
                    try
                    {
                        // Increment ping byte for each consecutive ping
                        buffer[0] = (byte)((PingByte + i) % 256);
                        var expectedByte = buffer[0];

                        // Ping-pong - Start receive first, then send to get accurate timing
                        var receiveTask = client.ReceiveAsync(buffer, SocketFlags.None, cts.Token).AsTask();

                        var stopwatch = Stopwatch.StartNew();

                        var sendTask = client.SendAsync(buffer, SocketFlags.None, cts.Token).AsTask();
                        var finished = await RunWithCancellation(sendTask, cts.Token);
                        if (!finished)
                        {
                            result.Error = cancellationToken.IsCancellationRequested ? ErrorCancelled : ErrorResponseTimeout;
                            return result;
                        }

                        finished = await RunWithCancellation(receiveTask, cts.Token);
                        if (!finished)
                        {
                            result.Error = cancellationToken.IsCancellationRequested ? ErrorCancelled : ErrorResponseTimeout;
                            return result;
                        }

                        var bytesRead = await receiveTask;
                        stopwatch.Stop();

                        if (bytesRead == 1 && buffer[0] == expectedByte)
                        {
                            totalPingMs += (int)stopwatch.ElapsedMilliseconds;
                            successfulPingCount++;
                        }
                        else
                        {
                            result.Error = ErrorInvalidResponse;
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug("Ping failure", ("exception", ex));
                        // Stop after first failed ping
                        break;
                    }
                }

                // Calculate average if at least one ping succeeded
                if (successfulPingCount > 0)
                {
                    result.RoundTripMs = totalPingMs / successfulPingCount;
                }
                else
                {
                    result.Error = cts.Token.IsCancellationRequested
                        ? (cancellationToken.IsCancellationRequested ? ErrorCancelled : ErrorResponseTimeout)
                        : ErrorResponseTimeout;
                }
            }
            catch (OperationCanceledException)
            {
                // Check if it was user cancellation or timeout
                result.Error = cancellationToken.IsCancellationRequested ? ErrorCancelled : ErrorResponseTimeout;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }

        private static async Task<bool> RunWithCancellation(Task task, CancellationToken cancellationToken)
        {
            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var finishedTask = await Task.WhenAny(task, cancellationTask);

            if (finishedTask == cancellationTask || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            return true;
        }

        private static Socket GetSocket(PingProtocol protocol)
        {
            if (protocol == PingProtocol.TCP)
            {
                return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }

            return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        private static IReadOnlyList<PingResult> CreateCancelledResults(IReadOnlyList<PingServer> servers, PingProtocol protocol)
        {
            return CreateErrorResults(servers, protocol, ErrorCancelled);
        }

        private static IReadOnlyList<PingResult> CreateErrorResults(
            IReadOnlyList<PingServer> servers, PingProtocol protocol,
            string error)
        {
            var results = new PingResult[servers.Count];
            for (var i = 0; i < servers.Count; i++)
            {
                results[i] = new PingResult
                {
                    Region = servers[i].Region,
                    Protocol = protocol,
                    Error = error
                };
            }
            return results;
        }

        private static bool ValidateServer(in PingServer server, ref PingResult result)
        {
            return server.Validate(out result.Error);
        }

        [Conditional("UNITY_WEBGL_BUILD")]
        private static void ThrowIfUnsupportedPlatform() =>
            throw new PlatformNotSupportedException("PingClient is currently not supported on WebGL platforms.");
    }
}
