// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Log;
    using Runtime;
    using Utils;
    using Logger = Log.Logger;

    /// <summary>
    /// Cloud service for fetching information about available regions.
    /// </summary>
    /// <remarks>
    /// Accessible via <see cref="CloudService.Regions"/>.
    /// </remarks>
    public class RegionsService
    {
        private const string Endpoint = "/regions";

        private readonly IRequestFactory requestFactory;
        private readonly IAuthClientInternal authClient;
        private readonly Logger logger = Log.GetLogger<RegionsService>();
        private readonly List<string> regionsLegacy = new();
        private bool isFetchingRegionsAsync;

        /// <summary>
        /// Gets the cached list of available regions from the last fetch operation.
        /// </summary>
        /// <value>A read-only list of region names.</value>
        internal IReadOnlyList<string> RegionsLegacy => regionsLegacy;

        /// <summary>
        /// Gets the cached region information including regions and ping servers from the last fetch operation.
        /// </summary>
        /// <value>The latest region fetch response containing regions and ping servers.</value>
        public Region[] Regions { get; private set; }

        public RegionsService(RequestFactory requestFactory, AuthClient authClient) : this(requestFactory, (IAuthClientInternal)authClient) { }

        internal RegionsService(IRequestFactory requestFactory, IAuthClientInternal authClient)
        {
            this.requestFactory = requestFactory;
            this.authClient = authClient;
        }

        /// <summary>
        /// Fetches available regions asynchronously.
        /// </summary>
        /// <param name="onRequestFinished">Callback invoked when the request completes with the list of regions.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        internal void FetchRegionsLegacy(Action<RequestResponse<IReadOnlyList<string>>> onRequestFinished, CancellationToken cancellationToken = default)
        {
            FetchRegions(response =>
            {
                var legacyResponse = new RequestResponse<IReadOnlyList<string>>
                {
                    Status = response.Status,
                    Exception = response.Exception,
                    Result = response.Result.Select(x => x.Name).ToArray()
                };
                onRequestFinished(legacyResponse);
            }, cancellationToken);
        }

        /// <summary>
        /// Fetches available regions asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a read-only list of region names.</returns>
        /// <exception cref="TaskCanceledException">Thrown when the operation is cancelled.</exception>
        /// <exception cref="ResponseDeserializationException">Thrown when the response cannot be deserialized.</exception>
        internal async Task<IReadOnlyList<string>> FetchRegionsLegacyAsync(CancellationToken cancellationToken = default)
        {
            await FetchRegionsAsync(cancellationToken);

            // The regions list is already updated in FetchRegionsAsync
            return regionsLegacy;
        }

        /// <summary>
        /// Gets the cooldown period before the next regions fetch request can be made.
        /// </summary>
        /// <returns>The remaining cooldown time as a <see cref="TimeSpan"/>.</returns>
        public TimeSpan GetFetchRegionsCooldown() => requestFactory.GetRequestCooldown(Endpoint, "GET");

        /// <summary>
        /// Fetches available regions and their associated ping servers asynchronously.
        /// </summary>
        /// <param name="onRequestFinished">Callback invoked when the request completes with region and ping server data.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        public void FetchRegions(Action<RequestResponse<Region[]>> onRequestFinished, CancellationToken cancellationToken = default)
            => FetchRegionsAsync(cancellationToken).Then(task =>
            {
                if (!task.IsCompletedSuccessfully)
                {
                    onRequestFinished(new() { Exception = task.Exception, Status = RequestStatus.Fail });
                    return;
                }

                onRequestFinished(new() { Status = RequestStatus.Success, Result = task.Result });
            }, TaskContinuationOptions.NotOnCanceled, cancellationToken);

        /// <summary>
        /// Fetches available regions and their associated ping servers asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="RegionFetchResponse"/> with regions and ping servers.</returns>
        /// <exception cref="TaskCanceledException">Thrown when the operation is cancelled.</exception>
        /// <exception cref="ResponseDeserializationException">Thrown when the response cannot be deserialized.</exception>
        public async Task<Region[]> FetchRegionsAsync(CancellationToken cancellationToken = default)
        {
            logger.Trace("FetchRegions - start");

            while (isFetchingRegionsAsync)
            {
                await Task.Yield();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException("The operation was canceled.", null, cancellationToken);
            }

            isFetchingRegionsAsync = true;

            const string requestName = nameof(RegionsService) + ". " + nameof(FetchRegionsAsync);
            var text = await requestFactory.SendRequestAsync(Endpoint, "GET", null, null, requestName, authClient.SessionToken);

            if (cancellationToken.IsCancellationRequested)
            {
                isFetchingRegionsAsync = false;
                throw new TaskCanceledException("The operation was canceled.", null, cancellationToken);
            }

            RegionFetchResponse response;

            try
            {
                response = CoherenceJson.DeserializeObject<RegionFetchResponse>(text);
                Regions = response;
                regionsLegacy.Clear();
                regionsLegacy.AddRange(response.Regions ?? Array.Empty<string>());

                logger.Trace("FetchRegions - end", ("regions count", response.Regions?.Length));
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FetchRegionsAsync)),
                    ("Response", text),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
            finally
            {
                isFetchingRegionsAsync = false;
            }

            return response;
        }
    }
}
