using ModbusOpcGateway.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ModbusOpcGateway.Core.Services
{
    /// <summary>
    /// 告警广播服务：将告警事件通过 SignalR 推送到所有客户端
    /// </summary>
    public class AlarmBroadcastService : BackgroundService
    {
        private readonly AlarmService _alarmService;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly ILogger _log;

        public AlarmBroadcastService(AlarmService alarmService, IHubContext<ScadaHub> hubContext)
        {
            _alarmService = alarmService;
            _hubContext = hubContext;
            _log = Log.ForContext<AlarmBroadcastService>();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _alarmService.AlarmTriggered += OnAlarmTriggered;
            _alarmService.AlarmRecovered += OnAlarmRecovered;

            _log.Information("[AlarmBroadcast] 告警广播服务已启动");

            var tcs = new TaskCompletionSource();
            stoppingToken.Register(() =>
            {
                _alarmService.AlarmTriggered -= OnAlarmTriggered;
                _alarmService.AlarmRecovered -= OnAlarmRecovered;
                tcs.TrySetResult();
            });

            return tcs.Task;
        }

        private async void OnAlarmTriggered(object? sender, AlarmTriggeredEventArgs e)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("AlarmTriggered", e.Record);
                _log.Debug("[AlarmBroadcast] 告警已广播: {AlarmId}", e.Record.Id);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[AlarmBroadcast] 广播告警失败");
            }
        }

        private async void OnAlarmRecovered(object? sender, AlarmTriggeredEventArgs e)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("AlarmRecovered", e.Record);
                _log.Debug("[AlarmBroadcast] 告警恢复已广播: {AlarmId}", e.Record.Id);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[AlarmBroadcast] 广播告警恢复失败");
            }
        }
    }
}
