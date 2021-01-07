using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;

namespace Netcool.EventBus.Mqtt
{
    public class MqttPersistentConnection : IMqttPersistentConnection
    {
        private bool _disposed;
        private readonly object _syncRoot = new object();

        private readonly ILogger<MqttPersistentConnection> _logger;
        private readonly ManagedMqttClientOptions _clientOptions;
        private readonly IManagedMqttClient _mqttClient;

        public MqttPersistentConnection(ILogger<MqttPersistentConnection> logger,
            IOptionsMonitor<EventBusMqttOptions> options)
        {
            _logger = logger;

            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(options.CurrentValue.ClientId)
                .WithTcpServer(options.CurrentValue.TcpIp, options.CurrentValue.TcpPort)
                .WithCleanSession(options.CurrentValue.CleanSession);
            if (!string.IsNullOrEmpty(options.CurrentValue.Username) &&
                !string.IsNullOrEmpty(options.CurrentValue.Password))
            {
                clientOptionsBuilder =
                    clientOptionsBuilder.WithCredentials(options.CurrentValue.Username, options.CurrentValue.Password);
            }

            _clientOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
                .WithClientOptions(clientOptionsBuilder.Build())
                .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _mqttClient?.StopAsync().Wait();
                _mqttClient?.Dispose();
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }

        public bool IsConnected => _mqttClient.IsConnected;

        public bool TryConnect()
        {
            lock (_syncRoot)
            {
                if (IsConnected)
                {
                    // prevent duplicated concurrent re-connection
                    return true;
                }

                if (!_mqttClient.IsStarted)
                {
                    _logger.LogInformation("Mqtt Client is trying to connect");
                    _mqttClient.StartAsync(_clientOptions).Wait();
                    _mqttClient.UseConnectedHandler(e => { _logger.LogInformation("MQTT Server connected!"); });
                    _mqttClient.UseDisconnectedHandler((e) =>
                    {
                        _logger.LogError("MQTT Server disconnected: " + e?.Exception?.Message);
                    });
                }


                return _mqttClient.IsConnected;
            }
        }

        public IManagedMqttClient GetClient()
        {
            return _mqttClient;
        }
    }
}