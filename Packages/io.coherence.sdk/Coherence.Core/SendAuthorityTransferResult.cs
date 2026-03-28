// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence
{
    using Connection;
    using Entities;
    using Log;

    /// <summary>
    /// Represents the result of an
    /// <see cref="IClient.SendAuthorityTransfer(Coherence.Entities.Entity,Coherence.Connection.ClientID,bool,Coherence.AuthorityType,out Coherence.SendAuthorityTransferResult)">authority transfer</see>
    /// over an entity.
    /// </summary>
    public readonly struct SendAuthorityTransferResult
    {
        internal static readonly SendAuthorityTransferResult Success = new(SendAuthorityTransferResultType.Success, null, null);

        public SendAuthorityTransferResultType Type { get; }
        public string FailureMessage { get; }
        internal Warning? Warning { get; }

        public SendAuthorityTransferResult(SendAuthorityTransferResultType type, Warning? warning, string failureMessage)
        {
            Type = type;
            Warning = warning;
            FailureMessage = failureMessage;
        }

        public static implicit operator SendAuthorityTransferResultType(SendAuthorityTransferResult result) => result.Type;
        public static implicit operator bool(SendAuthorityTransferResult result) => result.Type is SendAuthorityTransferResultType.Success;
    }

    /// <summary>
    /// Specifies the options for the result of an
    /// <see cref="IClient.SendAuthorityTransfer(Entity, ClientID, bool, AuthorityType, out SendAuthorityTransferResult)">authority transfer</see>
    /// over an entity.
    /// </summary>
    public enum SendAuthorityTransferResultType
    {
        Success = 1,
        InvalidAuthorityTypeError = 2
    }
}
