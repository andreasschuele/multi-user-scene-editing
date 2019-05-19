using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MUSE.Server.Utils
{
    /// <summary>
    /// The JsonUtils class contains helper functions to serialize and deserialize POCO objects to and from JSON formatted strings.
    /// </summary>
    public class JsonUtils
    {
        /// <summary>
        /// JSON serialization settings.
        /// </summary>
        private static JsonSerializerSettings _serializerSettings;

        static JsonUtils() {
            _serializerSettings = new JsonSerializerSettings();
            _serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        }

        /// <summary>
        /// Serialize an object to a JSON string.
        /// </summary>
        /// <param name="obj">An object.</param>
        /// <returns></returns>
        public static string SerializeObject(object obj)
        {
            return JsonConvert.SerializeObject(obj, _serializerSettings);
        }

        /// <summary>
        /// Deserialize an object from a JSON string.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="json">A JSON string.</param>
        /// <returns></returns>
        public static T DeserializeObject<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
