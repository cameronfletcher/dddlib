#pragma warning disable CS1591

namespace dddlib.Projections.MongoDB
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using dddlib.Projections.Sdk;
    using global::MongoDB.Bson.IO;
    using global::MongoDB.Bson.Serialization;

    public class MongoDBSerializer : ISerializer
    {
        public Task<T> DeserializeAsync<T>(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BsonBinaryReader(memoryStream))
            {
                return Task.FromResult(BsonSerializer.Deserialize<T>(reader));
            }
        }

        public Task<byte[]> SerializeAsync(object data)
        {
            Guard.Against.Null(() => data);

            using (var memoryStream = new MemoryStream())
            using (var writer = new BsonBinaryWriter(memoryStream))
            {
                BsonSerializer.Serialize(writer, data.GetType(), data);
                writer.Flush();
                return Task.FromResult(memoryStream.GetBuffer());
            }
        }
    }
}
