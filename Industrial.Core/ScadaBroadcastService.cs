using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Industrial.Core
{
    /// <summary>
    /// SCADA 数据广播服务：监听 SharedData 变更并通过 SignalR 广播到所有客户端
    /// </summary>
    public class ScadaBroadcastService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly ILogger _log;

        public ScadaBroadcastService(SharedData sharedData, IHubContext<ScadaHub> hubContext)
        {
            _sharedData = sharedData;
            _hubContext = hubContext;
            _log = Log.ForContext<ScadaBroadcastService>();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 订阅 SharedData 变更事件
            _sharedData.DataChanged += OnDataChanged;
            
            _log.Information("[ScadaBroadcast] Service started");
            
            // 保持服务运行直到取消
            var tcs = new TaskCompletionSource();
            stoppingToken.Register(() =>
            {
                _sharedData.DataChanged -= OnDataChanged;
                tcs.TrySetResult();
            });
            
            return tcs.Task;
        }

        private async void OnDataChanged(object? sender, DataChangedEventArgs e)
        {
            try
            {
                var snapshot = new DataSnapshot
                {
                    Temperature = e.Temperature,
                    Pressure = e.Pressure,
                    Status = e.Status,
                    Mode = (int)e.Mode,
                    NoiseMultiplier = e.NoiseMultiplier,
                    ResponseDelayMs = e.ResponseDelayMs
                };

                await _hubContext.Clients.All.SendAsync("DataUpdated", snapshot);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[ScadaBroadcast] Failed to broadcast data");
            }
        }
    }
}
