using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqMessageSerializer: IMessageSerializer
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public RabbitMqMessageSerializer()
        {
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                IgnoreNullValues = true
            };

            _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        public ReadOnlyMemory<byte> Serialize<T>(T message)
        {
            return JsonSerializer.SerializeToUtf8Bytes(message, _jsonSerializerOptions);
        }

        public T? Deserialize<T>(ReadOnlyMemory<byte> body)
        {
            var bytes = body.ToArray();
            return JsonSerializer.Deserialize<T>(bytes, _jsonSerializerOptions);
        }

        public string GetContentType<T>()
        {
            return "application/json";
        }
    }
}
