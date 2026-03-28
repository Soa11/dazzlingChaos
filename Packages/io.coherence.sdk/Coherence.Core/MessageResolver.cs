// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using Coherence.ProtocolDef;

    public class MessageResolver
    {
        private IEntityRegistry entityRegistry;

        public MessageResolver(IEntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public bool IsResolvable(IEntityMessage message)
        {
            if (message is IEntityCommand && message.Routing == MessageTarget.StateAuthorityOnly &&
                !entityRegistry.HasAuthorityOverEntity(message.Entity, AuthorityType.State))
            {
                return false;
            }

            if (!entityRegistry.EntityExists(message.Entity))
            {
                return false;
            }

            var entityRefs = message.GetEntityRefs();
            if (entityRefs == null)
            {
                return true;
            }

            foreach (var entity in message.GetEntityRefs())
            {
                if (!entity.IsValid)
                {
                    continue;
                }

                if (!entityRegistry.EntityExists(entity))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
