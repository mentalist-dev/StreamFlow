using System;

namespace StreamFlow
{
    public interface IMessageSerializer
    {
        ReadOnlyMemory<byte> Serialize<T>(T message);

        T? Deserialize<T>(ReadOnlyMemory<byte> body);

        T? Deserialize<T>(ReadOnlyMemory<byte> body, Type returnType);

        string GetContentType<T>();
    }
}
