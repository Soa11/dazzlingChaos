namespace Coherence.Cloud
{
    using System;

    [Obsolete("Use RegionsService instead. This class will be removed in a future version.")]
    public class RoomRegionsService : RegionsService
    {
        public RoomRegionsService(RequestFactory requestFactory, AuthClient authClient) : this(requestFactory, (IAuthClientInternal)authClient) { }
        internal RoomRegionsService(IRequestFactory requestFactory, IAuthClientInternal authClient) : base(requestFactory, authClient) { }
    }
}
