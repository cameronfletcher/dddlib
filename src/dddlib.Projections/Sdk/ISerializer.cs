using System.IO;
using System.Threading.Tasks;

namespace dddlib.Projections.Sdk
{
    /// <summary>
    /// Serialize/deserialize for repositories
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serializes the specified object.
        /// </summary>
        /// <param name="data">The object.</param>
        /// <returns>The bytes representation</returns>
        Task<byte[]> SerializeAsync(object data);

        /// <summary>
        /// Deserializes the specified stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">The serialized representation.</param>
        /// <returns>Deserialized object</returns>
         Task<T> DeserializeAsync<T>(byte[] data);
    }
}
