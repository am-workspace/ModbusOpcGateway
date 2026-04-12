using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusOpcGateway.Core
{
    /// <summary>
    /// MQTT Publisher 服务：将 SharedData 数据发布到 MQTT Broker
    /// </summary>
    public class MqttPublisherService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly MqttSettings _settings;
        private readonly ILogger _log;
        private IMqttClient? _mqttClient;
        private MqttClientOptions? _mqttOptions;

        public MqttPublisherService(
            SharedData sharedData,
            IOptionsMonitor<AppSettings> optionsMonitor)
        {
            _sharedData = sharedData;
            _settings = optionsMonitor.CurrentValue.Mqtt;
            _log = Log.ForContext("SourceContext", "MqttPublisher");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled)
            {
                _log.Information("[Mqtt] Publisher is disabled in configuration");
                return;
            }

            try
            {
                await ConnectAsync();
                _sharedData.DataChanged += OnDataChanged;

                // 等待停止信号
                var tcs = new TaskCompletionSource();
                stoppingToken.Register(() =>
                {
                    _sharedData.DataChanged -= OnDataChanged;
                    _ = DisconnectAsync();
                    _log.Information("[Mqtt] Publisher stopped");
                    tcs.TrySetResult();
                });

                await tcs.Task;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[Mqtt] Publisher failed to start");
            }
        }

        private async Task ConnectAsync()
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.Broker, _settings.Port)
                .WithClientId($"ModbusOpcGateway_{Guid.NewGuid():N}")
                .WithCleanSession();

            // 如果配置了用户名密码，添加认证
            if (!string.IsNullOrEmpty(_settings.Username))
            {
                optionsBuilder.WithCredentials(_settings.Username, _settings.Password ?? "");
                _log.Information("[Mqtt] Using authentication with username: {Username}", _settings.Username);
            }

            _mqttOptions = optionsBuilder.Build();

            await _mqttClient.ConnectAsync(_mqttOptions);
            _log.Information("[Mqtt] Connected to broker {Broker}:{Port}", _settings.Broker, _settings.Port);
        }

        private async Task DisconnectAsync()
        {
            if (_mqttClient?.IsConnected == true)
            {
                await _mqttClient.DisconnectAsync();
                _log.Information("[Mqtt] Disconnected from broker");
            }
        }

        private void OnDataChanged(object? sender, DataChangedEventArgs e)
        {
            _ = PublishDataAsync(e);
        }

        private async Task PublishDataAsync(DataChangedEventArgs e)
        {
            if (_mqttClient?.IsConnected != true)
            {
                _log.Warning("[Mqtt] Not connected to broker, skipping publish");
                return;
            }

            try
            {
                var timestamp = DateTime.UtcNow.ToString("O");
                var sensorId = "sensor001";

                // 发布 Temperature
                await PublishMessageAsync(
                    $"industrial/{sensorId}/temperature",
                    new { parameter = "temperature", value = e.Temperature, timestamp, sensorId });

                // 发布 Pressure
                await PublishMessageAsync(
                    $"industrial/{sensorId}/pressure",
                    new { parameter = "pressure", value = e.Pressure, timestamp, sensorId });

                // 发布 Status
                await PublishMessageAsync(
                    $"industrial/{sensorId}/status",
                    new { parameter = "status", value = e.Status, timestamp, sensorId });
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[Mqtt] Failed to publish data");
            }
        }

        private async Task PublishMessageAsync(string topic, object payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(JsonConvert.SerializeObject(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient!.PublishAsync(message);
            _log.Debug("[Mqtt] Published to {Topic}: {Payload}", topic, payload);
        }
    }
}
