using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Industrial.Core
{
    /// <summary>
    /// SignalR Hub：为 SCADA HMI 提供实时数据推送和远程控制接口
    /// </summary>
    public class ScadaHub : Hub
    {
        private readonly SharedData _sharedData;
        private readonly ILogger _log;

        public ScadaHub(SharedData sharedData)
        {
            _sharedData = sharedData;
            _log = Log.ForContext<ScadaHub>();
        }

        /// <summary>
        /// 客户端调用：获取当前数据快照
        /// </summary>
        public Task<DataSnapshot> GetDataSnapshot()
        {
            var (temp, press, status) = _sharedData.Snapshot();
            return Task.FromResult(new DataSnapshot
            {
                Temperature = temp,
                Pressure = press,
                Status = status,
                Mode = (int)_sharedData.GetMode(),
                NoiseMultiplier = _sharedData.GetNoiseMultiplier(),
                ResponseDelayMs = _sharedData.GetResponseDelayMs()
            });
        }

        /// <summary>
        /// 客户端调用：设置模拟模式
        /// </summary>
        public Task SetMode(int mode)
        {
            if (Enum.IsDefined(typeof(SharedData.SimulationMode), mode))
            {
                _sharedData.SetMode((SharedData.SimulationMode)mode);
                _log.Information("[SignalR] Mode changed to {Mode} by client {ConnectionId}", mode, Context.ConnectionId);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：设置噪声系数
        /// </summary>
        public Task SetNoiseMultiplier(float noise)
        {
            _sharedData.SetNoiseMultiplier(noise);
            _log.Information("[SignalR] Noise multiplier changed to {Noise} by client {ConnectionId}", noise, Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：设置响应延迟
        /// </summary>
        public Task SetResponseDelay(int delayMs)
        {
            _sharedData.SetResponseDelay(delayMs);
            _log.Information("[SignalR] Response delay changed to {DelayMs}ms by client {ConnectionId}", delayMs, Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：恢复正常
        /// </summary>
        public Task ResumeNormal()
        {
            _sharedData.ResumeNormal();
            _log.Information("[SignalR] Resume normal operation by client {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：注入异常温度
        /// </summary>
        public Task InjectFaultyTemperature()
        {
            _sharedData.InjectFaultyTemperature();
            _log.Warning("[SignalR] Faulty temperature injected by client {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：注入异常压力
        /// </summary>
        public Task InjectFaultyPressure()
        {
            _sharedData.InjectFaultyPressure();
            _log.Warning("[SignalR] Faulty pressure injected by client {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：冻结数据
        /// </summary>
        public Task FreezeData()
        {
            _sharedData.FreezeData();
            _log.Warning("[SignalR] Data frozen by client {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        public override Task OnConnectedAsync()
        {
            _log.Information("[SignalR] Client connected: {ConnectionId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _log.Information("[SignalR] Client disconnected: {ConnectionId}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }

    /// <summary>
    /// 数据快照传输对象
    /// </summary>
    public class DataSnapshot
    {
        public float Temperature { get; set; }
        public float Pressure { get; set; }
        public bool Status { get; set; }
        public int Mode { get; set; }
        public float NoiseMultiplier { get; set; }
        public int ResponseDelayMs { get; set; }
    }
}
