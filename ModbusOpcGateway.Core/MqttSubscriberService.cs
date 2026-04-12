using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using Serilog;
using System.Text.Json;

namespace ModbusOpcGateway.Core
{
    /// <summary>
    /// MQTT 订阅服务：订阅 industrial/# 主题，将数据写入 SharedData
    /// 数据流: MQTT Broker → MqttSubscriberService → SharedData.Update() → DataChanged 事件
    /// </summary>
    public class MqttSubscriberService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly MqttSettings _settings;
        private readonly ILogger _log;
        private IMqttClient? _mqttClient;
        private MqttClientOptions? _mqttOptions;

        public MqttSubscriberService(
            SharedData sharedData,
            IOptionsMonitor<AppSettings> optionsMonitor)
        {
            _sharedData = sharedData;
            _settings = optionsMonitor.CurrentValue.Mqtt;
            _log = Log.ForContext("SourceContext", "MqttSubscriber");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled)
            {
                _log.Information("[MqttSubscriber] 服务已禁用");
                return;
            }

            try
            {
                await ConnectAndSubscribeAsync(stoppingToken);
                _log.Information("[MqttSubscriber] 服务已启动，订阅 industrial/modbus/#");

                // 保持运行直到取消
                var tcs = new TaskCompletionSource();
                stoppingToken.Register(() =>
                {
                    _ = DisconnectAsync();
                    tcs.TrySetResult();
                });

                await tcs.Task;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[MqttSubscriber] 服务启动失败");
            }
        }

        private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.Broker, _settings.Port)
                .WithClientId($"BlazorScadaHmi_{Guid.NewGuid():N}")
                .WithCleanSession();

            // 条件式认证
            if (!string.IsNullOrEmpty(_settings.Username))
            {
                optionsBuilder.WithCredentials(_settings.Username, _settings.Password ?? "");
            }

            _mqttOptions = optionsBuilder.Build();

            // 配置消息接收处理器
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            // 配置连接状态变更
            _mqttClient.DisconnectedAsync += e =>
            {
                _log.Warning("[MqttSubscriber] 连接断开，尝试重连...");
                return Task.CompletedTask;
            };

            _mqttClient.ConnectedAsync += e =>
            {
                _log.Information("[MqttSubscriber] 已连接到 {Broker}:{Port}", _settings.Broker, _settings.Port);
                return Task.CompletedTask;
            };

            await _mqttClient.ConnectAsync(_mqttOptions, cancellationToken);

            // 订阅 industrial/modbus/# 主题（来自 PlcGateway 的 Modbus 采集数据）
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter("industrial/modbus/#", MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            
            await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
        }

        private async Task DisconnectAsync()
        {
            if (_mqttClient?.IsConnected == true)
            {
                await _mqttClient.DisconnectAsync();
                _log.Information("[MqttSubscriber] 已断开连接");
            }
        }

        /// <summary>
        /// 处理接收到的 MQTT 消息
        /// </summary>
        private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.ConvertPayloadToString();

                _log.Debug("[MqttSubscriber] 收到消息: {Topic} = {Payload}", topic, payload);

                // 解析 Topic: industrial/{source}/{parameter}
                var parts = topic.Split('/');
                if (parts.Length < 3)
                {
                    _log.Warning("[MqttSubscriber] 无效的 Topic 格式: {Topic}", topic);
                    return;
                }

                var parameter = parts[2].ToLowerInvariant();

                // 解析 JSON payload
                using var jsonDoc = JsonDocument.Parse(payload);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("value", out var valueElement))
                {
                    _log.Warning("[MqttSubscriber] Payload 缺少 value 字段: {Payload}", payload);
                    return;
                }

                // 根据参数类型更新 SharedData
                UpdateSharedData(parameter, valueElement);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[MqttSubscriber] 处理消息失败");
            }
        }

        /// <summary>
        /// 根据参数名称更新 SharedData
        /// </summary>
        private void UpdateSharedData(string parameter, JsonElement valueElement)
        {
            var (temp, press, status) = _sharedData.Snapshot();

            switch (parameter)
            {
                case "temperature":
                    temp = valueElement.GetSingle();
                    _log.Debug("[MqttSubscriber] 更新温度: {Temperature}", temp);
                    break;

                case "pressure":
                    press = valueElement.GetSingle();
                    _log.Debug("[MqttSubscriber] 更新压力: {Pressure}", press);
                    break;

                case "status":
                    status = valueElement.GetBoolean();
                    _log.Debug("[MqttSubscriber] 更新状态: {Status}", status);
                    break;

                default:
                    _log.Debug("[MqttSubscriber] 忽略未知参数: {Parameter}", parameter);
                    return;
            }

            // 批量更新，触发 DataChanged 事件
            _sharedData.Update(temp, press, status);
        }
    }
}
