﻿using System;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqChannel: IDisposable
    {
        public RabbitMqChannel(IConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Channel = connection.CreateModel();
        }

        public IConnection Connection { get; }
        public IModel Channel { get; }

        public void Dispose()
        {
            Channel.Dispose();
            Connection.Dispose();
        }
    }
}