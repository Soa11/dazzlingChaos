#if UNITY_5_3_OR_NEWER

namespace Coherence.Cloud.Coroutines
{
    using Cloud;

    public static class RegionsServiceCoroutineExtensions
    {
        /// <summary>
        /// Creates a coroutine that waits for the fetch regions info operation to complete.
        /// </summary>
        /// <param name="regionsService">The room regions service instance.</param>
        /// <returns>A coroutine that yields until the regions and ping servers are fetched.</returns>
        public static WaitForRequestResponse<Region[]> WaitForFetchRegions(this RegionsService regionsService)
            => new(onRequestFinished => regionsService.FetchRegions(onRequestFinished));
    }
}

#endif
