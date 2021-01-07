using System;
using MQTTnet.Extensions.ManagedClient;

namespace Netcool.EventBus.Mqtt
{
    public interface IMqttPersistentConnection
        : IDisposable
    {
        bool IsConnected { get; }

        bool TryConnect();

        IManagedMqttClient GetClient();
    }
}