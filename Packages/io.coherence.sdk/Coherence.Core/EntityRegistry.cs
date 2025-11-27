// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using Entities;
    using System.Collections.Generic;

    public interface IEntityRegistry
    {
        bool EntityExists(Entity entity);
        bool HasAuthorityOverEntity(Entity entity, AuthorityType authorityType);
    }

    internal class EntityRegistry : IEntityRegistry
    {
        private readonly HashSet<Entity> knownEntities;
        private readonly Dictionary<Entity, AuthorityType> authorityByEntity;

        public EntityRegistry(HashSet<Entity> knownEntities, Dictionary<Entity, AuthorityType> authorityByEntity)
        {
            this.knownEntities = knownEntities;
            this.authorityByEntity = authorityByEntity;
        }

        public bool EntityExists(Entity entity)
        {
            return knownEntities.Contains(entity);
        }

        public bool HasAuthorityOverEntity(Entity entity, AuthorityType authorityType)
        {
            return authorityByEntity.TryGetValue(entity, out var authType) && authType.Contains(authorityType);
        }
    }
}
