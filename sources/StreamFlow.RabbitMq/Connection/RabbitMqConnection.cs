using System.Diagnostics;
using System.Reflection;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq.Connection;

public interface IRabbitMqConnection
{
    IConnection Create();
}

public class RabbitMqConnection: IRabbitMqConnection
{
    private static readonly string StreamFlowVersion;

    private readonly string[] _hostNames;
    private readonly string _serviceId;
    private readonly ConnectionFactory _connectionFactory;

    static RabbitMqConnection()
    {
        StreamFlowVersion = GetStreamFlowVersion();
    }

    public RabbitMqConnection(string[] hostNames, string userName, string password, string virtualHost, string? serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            serviceId = "StreamFlow";
        }

        _hostNames = hostNames;
        _serviceId = serviceId;
        _connectionFactory = CreateConnectionFactory(userName, password, virtualHost, serviceId);
    }

    public IConnection Create()
    {
        var endpoints = _hostNames
            .Select(node =>
            {
                var parts = node.Split(":", StringSplitOptions.RemoveEmptyEntries);

                var hostName = parts[0];

                if (parts.Length == 1)
                    return new AmqpTcpEndpoint(hostName);

                var port = int.Parse(parts[1]);

                return new AmqpTcpEndpoint(hostName, port);
            }).ToArray();

        var endpointResolver = new DefaultEndpointResolver(endpoints);
        var connection = _connectionFactory.CreateConnection(endpointResolver, _serviceId);
        return connection;
    }

    private ConnectionFactory CreateConnectionFactory(string userName, string password, string virtualHost, string? serviceId)
    {
        var entryAssembly = GetEntryAssembly();
        var entryAssemblyName = entryAssembly.GetName().Name ?? "unable to resolve assembly name";
        var entryAssemblyVersion = GetAssemblyVersion(entryAssembly);

        var connectionFactory = new ConnectionFactory
        {
            HostName = "does-not-exist",
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProperties =
            {
                ["copyright"] = "mentalist.dev",
                ["information"] = "Licensed under the Apache License version 2.0",
                ["client_library"] = "StreamFlow.RabbitMq",
                ["client_library_version"] = StreamFlowVersion,
                ["entry_assembly"] = entryAssemblyName,
                ["entry_assembly_version"] = entryAssemblyVersion,
                ["stream_flow_service_id"] = serviceId
            }
        };
        return connectionFactory;
    }

    private Assembly GetEntryAssembly()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
        {
            entryAssembly = new StackTrace().GetFrames().LastOrDefault()?.GetMethod()?.Module.Assembly;
            if (entryAssembly == null)
            {
                entryAssembly = Assembly.GetCallingAssembly();
            }
        }
        return entryAssembly;
    }

    private string GetAssemblyVersion(Assembly? entryAssembly)
    {
        var version = string.Empty;
        try
        {
            var attribute = entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute != null)
            {
                return attribute.InformationalVersion;
            }
        }
        catch
        {
            version = string.Empty;
        }

        return version;
    }

    private static string GetStreamFlowVersion()
    {
        var version = string.Empty;
        try
        {
            var entryAssembly = Assembly.GetAssembly(typeof(RabbitMqConnection));
            var attribute = entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute != null)
            {
                return attribute.InformationalVersion;
            }
        }
        catch
        {
            version = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            version = Assembly.GetAssembly(typeof(RabbitMqConnection))?.GetName().Version?.ToString() ?? "0.0.0.0";
        }

        return version;
    }
}
