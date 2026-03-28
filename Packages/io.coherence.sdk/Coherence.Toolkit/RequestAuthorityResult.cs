// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using Log;

    /// <summary>
    /// Represents the result of a <see cref="CoherenceSync.RequestAuthority">request to acquire authority</see>
    /// over an entity.
    /// </summary>
    public readonly struct RequestAuthorityResult
    {
        internal static readonly RequestAuthorityResult Success = new(RequestAuthorityResultType.Success, null, null);

        public RequestAuthorityResultType Type { get; }
        public string FailureMessage { get; }
        internal Warning? Warning { get; }

        public RequestAuthorityResult(RequestAuthorityResultType type, Warning? warning, string failureMessage)
        {
            Type = type;
            Warning = warning;
            FailureMessage = failureMessage;
        }

        public static implicit operator RequestAuthorityResultType(RequestAuthorityResult result) => result.Type;
        public static implicit operator bool(RequestAuthorityResult result) => result.Type is RequestAuthorityResultType.Success;

        internal void LogFailure(Logger logger)
        {
            if (Warning is { } warning)
            {
                logger?.Warning(warning, FailureMessage);
            }
            else if (FailureMessage is not null)
            {
                logger?.Debug(FailureMessage);
            }
        }
    }

    /// <summary>
    /// Specifies the options for the result of a <see cref="CoherenceSync.RequestAuthority">request to acquire authority</see>
    /// over an entity.
    /// </summary>
    public enum RequestAuthorityResultType
    {
        Success = 1,
        Canceled = 2,
        EntityOrphanedError = 3,
        EntityNotTransferableError = 4,
        RequestRejectedError = 5,
        EntityNotSynchronizedWithNetwork = 6,
        InvalidAuthorityTypeError = 7,
        AlreadyHasAuthorityError = 8,
        EntityIsClientConnectionError = 9,
        TimeoutError = 10
    }
}
