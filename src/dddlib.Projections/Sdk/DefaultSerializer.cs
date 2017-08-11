namespace dddlib.Projections.Sdk
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Script.Serialization;

    /// <summary>
    /// Default serializer using <see cref="System.Web.Script.Serialization.JavaScriptSerializer"/>
    /// </summary>
    /// <seealso cref="dddlib.Projections.Sdk.ISerializer" />
    public class DefaultSerializer : ISerializer
    {
        private readonly JavaScriptSerializer serializer = CreateSerializer();

        /// <summary>
        /// Deserializes the specified stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">The serialized representation.</param>
        /// <returns>
        /// Deserialized object
        /// </returns>
        public Task<T> DeserializeAsync<T>(byte[] data) => Task.FromResult(this.serializer.Deserialize<T>(Encoding.UTF8.GetString(data)));

        /// <summary>
        /// Serializes the specified object.
        /// </summary>
        /// <param name="data">The object.</param>
        /// <returns>
        /// The bytes representation
        /// </returns>
        public Task<byte[]> SerializeAsync(object data) => Task.FromResult(Encoding.UTF8.GetBytes(this.serializer.Serialize(data)));

        private static JavaScriptSerializer CreateSerializer()
        {
            var serializer = new JavaScriptSerializer();
            serializer.RegisterConverters(new[] { new DateTimeConverter() });
            return serializer;
        }
    }
}
