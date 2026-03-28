// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_WEBGL && !UNITY_EDITOR
#define USE_WEB_TRANSPORT
#endif

namespace Coherence.Transport
{
    using Log;
    using Stats;
    using System;

#if USE_WEB_TRANSPORT
    using Coherence.Transport.Web;
#endif

    public interface ITransportFactory
    {
        ITransport Create(ushort mtu, IStats stats, Logger logger);
    }

    public class DefaultTransportFactory : ITransportFactory
    {
        private readonly TransportType type;

        public DefaultTransportFactory(TransportType type = TransportType.UDPWithTCPFallback)
        {
            this.type = type;
        }

        public ITransport Create(ushort mtu, IStats stats, Logger logger)
        {
#if USE_WEB_TRANSPORT
            return new WebTransport(WebInterop.InitializeConnection, WebInterop.WebConnect, WebInterop.WebDisconnect, WebInterop.WebSend, stats, logger);
#else
            switch (type)
            {
                case TransportType.UDPWithTCPFallback:
                    return new CompoundTransport<UdpTransportV2, TcpTransport>(
                        new UdpTransportV2(mtu, stats, logger),
                        new TcpTransport(stats, logger),
                        logger);
                case TransportType.UDPOnly:
                    return new UdpTransportV2(mtu, stats, logger);
                case TransportType.TCPOnly:
                    return new TcpTransport(stats, logger);
                case TransportType.MemoryOnly:
                    return new MemoryTransport(mtu, stats, logger);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, $"Invalid {nameof(TransportType)}");
            }
#endif
        }
    }
}
