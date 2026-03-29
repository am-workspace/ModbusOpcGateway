using Industrial.Core.Models;
using Industrial.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Industrial.Core
{
    /// <summary>
    /// SignalR Hub：为 SCADA HMI 提供实时数据推送和远程控制接口
    /// 权限控制在方法级别配置，允许匿名连接但控制操作需要授权
    /// </summary>
    public class ScadaHub : Hub
    {
        private readonly SharedData _sharedData;
        private readonly AlarmService _alarmService;
        private readonly ILogger _log;

        public ScadaHub(SharedData sharedData, AlarmService alarmService)
        {
            _sharedData = sharedData;
            _alarmService = alarmService;
            _log = Log.ForContext<ScadaHub>();
        }

        /// <summary>
        /// 客户端调用：获取当前数据快照
        /// </summary>
        [AllowAnonymous]
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
        [Authorize(Roles = "Operator,Admin")]
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
        [Authorize(Roles = "Operator,Admin")]
        public Task SetNoiseMultiplier(float noise)
        {
            _sharedData.SetNoiseMultiplier(noise);
            _log.Information("[SignalR] Noise multiplier changed to {Noise} by client {ConnectionId}", noise, Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：设置响应延迟
        /// </summary>
        [Authorize(Roles = "Operator,Admin")]
        public Task SetResponseDelay(int delayMs)
        {
            _sharedData.SetResponseDelay(delayMs);
            _log.Information("[SignalR] Response delay changed to {DelayMs}ms by client {ConnectionId}", delayMs, Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：恢复正常
        /// </summary>
        [Authorize(Roles = "Operator,Admin")]
        public Task ResumeNormal()
        {
            _sharedData.ResumeNormal();
            _log.Information("[SignalR] Resume normal operation by client {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：注入异常温度
        /// </summary>
        [Authorize(Roles = "Admin")]
        public Task InjectFaultyTemperature()
        {
            _sharedData.InjectFaultyTemperature();
            _log.Warning("[SignalR] Faulty temperature injected by client {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：注入异常压力
        /// </summary>
        [Authorize(Roles = "Admin")]
        public Task InjectFaultyPressure()
        {
            _sharedData.InjectFaultyPressure();
            _log.Warning("[SignalR] Faulty pressure injected by client {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 客户端调用：冻结数据
        /// </summary>
        [Authorize(Roles = "Admin")]
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

        // ==================== 告警相关接口 ====================

        /// <summary>
        /// 客户端调用：获取当前活动告警
        /// </summary>
        [AllowAnonymous]
        public Task<List<AlarmRecord>> GetActiveAlarms()
        {
            return Task.FromResult(_alarmService.GetActiveAlarms());
        }

        /// <summary>
        /// 客户端调用：获取告警历史
        /// </summary>
        [AllowAnonymous]
        public Task<List<AlarmRecord>> GetAlarmHistory(int count = 50)
        {
            return Task.FromResult(_alarmService.GetAlarmHistory(count));
        }

        /// <summary>
        /// 客户端调用：确认告警
        /// </summary>
        [Authorize(Roles = "Operator,Admin")]
        public Task<bool> AcknowledgeAlarm(string alarmId)
        {
            var result = _alarmService.AcknowledgeAlarm(alarmId, Context.ConnectionId);
            return Task.FromResult(result);
        }

        /// <summary>
        /// 客户端调用：获取告警规则
        /// </summary>
        [AllowAnonymous]
        public Task<List<AlarmRule>> GetAlarmRules()
        {
            return Task.FromResult(_alarmService.GetRules());
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
