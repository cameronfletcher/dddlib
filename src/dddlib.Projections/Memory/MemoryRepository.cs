// <copyright file="MemoryRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Projections.Memory
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Web.Script.Serialization;
    using dddlib.Projections.Sdk;

    /// <summary>
    /// Represents a memory repository.
    /// </summary>
    /// <typeparam name="TIdentity">The type of the identity.</typeparam>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <seealso cref="dddlib.Projections.IRepository{TIdentity, TEntity}" />
    public sealed class MemoryRepository<TIdentity, TEntity> : IRepository<TIdentity, TEntity>
        where TEntity : class
    {
        // horrible, this object shouldn't be allowed anywhere
        private static readonly ISerializer DefaultSerializer = new DefaultSerializer();

        private readonly ConcurrentDictionary<TIdentity, byte[]> entities;
        private readonly ISerializer serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryRepository{TIdentity, TEntity}"/> class.
        /// </summary>
        public MemoryRepository()
            : this(EqualityComparer<TIdentity>.Default, DefaultSerializer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryRepository{TIdentity, TEntity}"/> class.
        /// </summary>
        /// <param name="equalityComparer">The equality comparer.</param>
        /// <param name="serializer">The serializer</param>
        public MemoryRepository(IEqualityComparer<TIdentity> equalityComparer, ISerializer serializer)
        {
            Guard.Against.Null(() => equalityComparer);
            Guard.Against.Null(() => serializer);

            this.entities = new ConcurrentDictionary<TIdentity, byte[]>(equalityComparer);
            this.serializer = serializer;
        }

        /// <summary>
        /// Gets the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <returns>The entity.</returns>
        public async Task<TEntity> GetAsync(TIdentity identity) => this.entities.TryGetValue(identity, out var serializedEntity) ? await this.serializer.DeserializeAsync<TEntity>(serializedEntity) : null;

        /// <summary>
        /// Adds or updates the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="entity">The entity to add or update.</param>
        public async Task AddOrUpdateAsync(TIdentity identity, TEntity entity)
        {
            var serializedEntity = await this.serializer.SerializeAsync(entity);
            this.entities.AddOrUpdate(identity, serializedEntity, (i, e) => serializedEntity);
        }

        /// <summary>
        /// Removes the entity with the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <returns>Returns <c>true</c> if the entity was successfully removed; otherwise <c>false</c>.</returns>
        public Task<bool> RemoveAsync(TIdentity identity) => Task.FromResult(this.entities.TryRemove(identity, out _));

        /// <summary>
        /// Purges the contents of repository.
        /// </summary>
        public Task PurgeAsync()
        {
            this.entities.Clear();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs a bulk update against the contents of the repository.
        /// </summary>
        /// <param name="addOrUpdate">The entities to add or update.</param>
        /// <param name="remove">The identities of the entities to remove.</param>
        public async Task BulkUpdateAsync(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove)
        {
            foreach (var item in addOrUpdate)
            {
                var serializedEntity = await this.serializer.SerializeAsync(item.Value);
                this.entities.AddOrUpdate(item.Key, serializedEntity, (i, e) => serializedEntity);
            }

            foreach (var identity in remove)
            {
                this.entities.TryRemove(identity, out _);
            }
        }
    }
}
