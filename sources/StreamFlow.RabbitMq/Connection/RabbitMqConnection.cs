using System.Diagnostics;
using System.Linq;
using System.Reflection;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq.Connection
{
    public interface IRabbitMqConnection
    {
        IConnection Create();
    }

    public class RabbitMqConnection: IRabbitMqConnection
    {
        private static readonly string StreamFlowVersion;

        private readonly string _hostName;
        private readonly string _serviceId;
        private readonly ConnectionFactory _connectionFactory;

        static RabbitMqConnection()
        {
            StreamFlowVersion = GetStreamFlowVersion();
        }

        public RabbitMqConnection(string hostName, string userName, string password, string virtualHost, string? serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                serviceId = "StreamFlow";
            }

            _hostName = hostName;
            _serviceId = serviceId;
            _connectionFactory = CreateConnectionFactory(hostName, userName, password, virtualHost, serviceId);
        }

        public IConnection Create()
        {
            var endpoints = new DefaultEndpointResolver(new AmqpTcpEndpoint[] {new (_hostName)});
            var connection = _connectionFactory.CreateConnection(endpoints, _serviceId);
            return connection;
        }

        private ConnectionFactory CreateConnectionFactory(string hostName, string userName, string password, string virtualHost, string? serviceId)
        {
            var entryAssembly = GetEntryAssembly();
            var entryAssemblyName = entryAssembly?.GetName().Name ?? "unknown";
            var entryAssemblyVersion = GetAssemblyVersion(entryAssembly);

            var connectionFactory = new ConnectionFactory
            {
                HostName = hostName,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = false,
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
                entryAssembly = new StackTrace().GetFrames()?.LastOrDefault()?.GetMethod()?.Module.Assembly;
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
}
