using System;

namespace StreamFlow
{
    public interface IMessageSerializer
    {
        ReadOnlyMemory<byte> Serialize<T>(T message);

        T? Deserialize<T>(ReadOnlyMemory<byte> body);

        string GetContentType<T>();
    }
}