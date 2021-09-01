using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqMessageSerializer: IMessageSerializer
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions;

        static RabbitMqMessageSerializer()
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                IgnoreNullValues = true
            };

            JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        public ReadOnlyMemory<byte> Serialize<T>(T message)
        {
            return JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions);
        }

        public T? Deserialize<T>(ReadOnlyMemory<byte> body)
        {
            var bytes = body.ToArray();
            return JsonSerializer.Deserialize<T>(bytes, JsonSerializerOptions);
        }

        public string GetContentType<T>()
        {
            return "application/json";
        }
    }
}
