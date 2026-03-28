// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence
{
    using System;

    /// <summary>Represents the type of control client has over an entity.</summary>
    public enum AuthorityType : byte
    {
        /// <summary>No control of an entity. Neither inputs not entity state changes will be accepted by the Replication Server.</summary>
        None = 0,

        /// <summary>
        ///     Client is in charge of the state, that is, changes to the state of an entity will be accepted by the Replication
        ///     Server.
        /// </summary>
        State,

        /// <summary>
        ///     Client is in charge of the input, that is, inputs produced by this entity will be forwarded to the state authority.
        ///     Changes to the state of an entity will not be accepted by the Replication Server.
        /// </summary>
        Input,

        /// <summary>Client has both input and state authority over an entity.</summary>
        Full
    }

    public static class AuthorityModeExtensions
    {
        /// <summary>
        ///     Returns true if a given <see cref="AuthorityType" /> has sufficient rights to transfer specific
        ///     <see cref="AuthorityType" />.
        /// </summary>
        public static bool CanTransfer(this AuthorityType authorityType, AuthorityType other)
        {
            // Transferring | Full | State | Input |
            // -------------------------------------
            //      {  Full |  Ok  |  Ok   |  Ok   |
            // Got  { State |  Ok! |  Ok   |  Ok!  |
            //      { Input |  No  |  No   |  Ok   |

            if (authorityType == AuthorityType.None)
            {
                return false;
            }

            if (authorityType == other)
            {
                return true;
            }

            return authorityType.ControlsState();
        }

        public static AuthorityType Subtract(this AuthorityType authorityType, AuthorityType other)
        {
            switch (authorityType)
            {
                case AuthorityType.None:
                    return AuthorityType.None;
                case AuthorityType.Input:
                    return other.Contains(AuthorityType.Input) ? AuthorityType.None : AuthorityType.Input;
                case AuthorityType.State:
                    return other.Contains(AuthorityType.State) ? AuthorityType.None : AuthorityType.State;
                case AuthorityType.Full:
                    switch (other)
                    {
                        case AuthorityType.None:
                            return AuthorityType.Full;
                        case AuthorityType.Full:
                            return AuthorityType.None;
                        case AuthorityType.Input:
                            return AuthorityType.State;
                        case AuthorityType.State:
                            return AuthorityType.Input;
                        default:
                            throw new ArgumentException($"Unsupported {nameof(AuthorityType)}", nameof(other));
                    }
                default:
                    throw new ArgumentException($"Unsupported {nameof(AuthorityType)}", nameof(authorityType));
            }
        }

        internal static AuthorityType Union(this AuthorityType authorityType, AuthorityType other)
        {
            if (authorityType is AuthorityType.Full || other is AuthorityType.Full)
            {
                return AuthorityType.Full;
            }

            if (authorityType is AuthorityType.None)
            {
                return other;
            }

            if (other is AuthorityType.None)
            {
                return authorityType;
            }

            return authorityType != other ? AuthorityType.Full : authorityType;
        }

        /// <summary>Returns true if <paramref name="other" /> is a subset of this authority type.</summary>
        public static bool Contains(this AuthorityType authorityType, AuthorityType other)
            => authorityType == other
            || (authorityType == AuthorityType.Full && other is AuthorityType.State or AuthorityType.Input);

        /// <summary>Returns true if this type of authority controls state.</summary>
        public static bool ControlsState(this AuthorityType authorityType) =>
            authorityType is AuthorityType.State or AuthorityType.Full;

        /// <summary>Returns true if this type of authority controls input.</summary>
        public static bool ControlsInput(this AuthorityType authorityType) =>
            authorityType is AuthorityType.Input or AuthorityType.Full;
    }
}
