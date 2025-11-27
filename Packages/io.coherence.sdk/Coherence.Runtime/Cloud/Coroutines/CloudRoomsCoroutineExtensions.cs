#if UNITY_5_3_OR_NEWER

namespace Coherence.Cloud.Coroutines
{
    using Cloud;
    using System;
    using System.Collections.Generic;

    public static class CloudRoomsCoroutineExtensions
    {
        public static WaitForPredicate WaitForCloudConnection(this CloudRooms cloudService) => new(() => cloudService.IsConnectedToCloud);
        public static WaitForPredicate WaitForLogin(this CloudRooms cloudRooms) => new(() => cloudRooms.IsLoggedIn);

        /// <summary>
        /// Creates a coroutine that waits for the refresh regions operation to complete.
        /// </summary>
        /// <param name="cloudRooms">The CloudRooms instance.</param>
        /// <returns>A coroutine that yields until the regions are fetched.</returns>
        [Obsolete("This method is deprecated and will be removed in a future version. Use " + nameof(RegionsServiceCoroutineExtensions) + "." + nameof(RegionsServiceCoroutineExtensions.WaitForFetchRegions) + " instead.")]
        [Deprecated("09/09/2025", 1, 8, 0, Reason = "WaitForFetchRegionsInfo returns regions and ping servers")]
        public static WaitForRequestResponse<IReadOnlyList<string>> WaitForFetchRegions(this CloudRooms cloudRooms) => new((fn) => cloudRooms.RefreshRegions(fn));
    }
}

#endif
