#pragma warning disable CS1591

namespace Hawkeye.Components.Projections.MongoDB
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using dddlib.Projections;
    using global::MongoDB.Bson;
    using global::MongoDB.Bson.Serialization.Attributes;
    using global::MongoDB.Driver;

    public sealed class MongoDBRepository<TIdentity, TEntity> : IRepository<TIdentity, TEntity>
        where TEntity : class
    {
        private static readonly FindOneAndReplaceOptions<Document> UpdateOptions = new FindOneAndReplaceOptions<Document> { IsUpsert = true };

        private readonly IMongoDatabase database;
        private readonly IMongoCollection<Document> collection;

        public MongoDBRepository(MongoUrl mongoUrl, string schema)
        {
            Guard.Against.Null(() => mongoUrl);

            var client = new MongoClient(mongoUrl);

            this.database = client.GetDatabase(mongoUrl.DatabaseName);
            this.collection = this.database.GetCollection<Document>(string.Join(".", schema, typeof(TEntity).Name));
        }
    
        public async Task<TEntity> GetAsync(TIdentity identity)
        {
            var filter = Builders<Document>.Filter.Eq(x => x.Identity, identity);
            var document = await this.collection.Find(filter).FirstOrDefaultAsync();
            return document?.Entity;
        }

        public async Task AddOrUpdateAsync(TIdentity identity, TEntity entity)
        {
            var document = new Document { Identity = identity, Entity = entity };
            var filter = Builders<Document>.Filter.Eq(x => x.Identity, identity);
            await this.collection.FindOneAndReplaceAsync(filter, document, UpdateOptions);
        }

        public async Task<bool> RemoveAsync(TIdentity identity)
        {
            var filter = Builders<Document>.Filter.Eq(x => x.Identity, identity);
            var document = await this.collection.FindOneAndDeleteAsync(filter);
            return document != null;
        }

        public async Task PurgeAsync()
        {
            var name = this.collection.CollectionNamespace.CollectionName;

            await this.database.DropCollectionAsync(name);
            await this.database.CreateCollectionAsync(name);
        }

        public async Task BulkUpdateAsync(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove)
        {
            var requests = new List<WriteModel<Document>>();
            foreach(var kvp in addOrUpdate)
            {

                requests.Add(
                    new ReplaceOneModel<Document>(
                        Builders<Document>.Filter.Eq(x => x.Identity, kvp.Key),
                        new Document { Identity = kvp.Key, Entity = kvp.Value })
                    { IsUpsert = true });
            }

            foreach(var identity in remove)
            {
                requests.Add(new DeleteOneModel<Document>(Builders<Document>.Filter.Eq(x => x.Identity, identity)));
            }

            await this.collection.BulkWriteAsync(requests);
        }

        private sealed class Document
        {
            [BsonId]
            [BsonRepresentation(BsonType.String)]
            public TIdentity Identity { get; set; }

            public TEntity Entity { get; set; }
        }
    }
}