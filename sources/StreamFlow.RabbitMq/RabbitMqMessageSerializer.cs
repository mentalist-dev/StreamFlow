using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamFlow.RabbitMq;

public class RabbitMqMessageSerializer: IMessageSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static RabbitMqMessageSerializer()
    {
        JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public ReadOnlyMemory<byte> Serialize<T>(T message)
    {
        var messageType = message?.GetType() ?? typeof(T);
        return JsonSerializer.SerializeToUtf8Bytes(message, messageType, JsonSerializerOptions);
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> body)
    {
        return Deserialize<T>(body, typeof(T));
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> body, Type returnType)
    {
        return (T?)JsonSerializer.Deserialize(body.Span, returnType, JsonSerializerOptions);
    }

    public string GetContentType()
    {
        return "application/json";
    }
}
