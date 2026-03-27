using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernGateway
{
    /// <summary>
    /// 数据生成服务：后台持续产生模拟传感器数据（温度、压力、状态）。
    /// </summary>
    public class GeneratorService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
        private SimulationSettings _currentSimConfig;
        private readonly ITimeProvider _timeProvider;
        private readonly IRandomProvider _randomProvider;
        private readonly ILogger _log;

        public GeneratorService(
            SharedData sharedData,
            IOptionsMonitor<AppSettings> optionsMonitor)
        {
            _sharedData = sharedData;
            _optionsMonitor = optionsMonitor;
            _currentSimConfig = optionsMonitor.CurrentValue.Simulation;
            _timeProvider = new TimeProvider();
            _randomProvider = new RandomProvider();
            _log = Log.ForContext("SourceContext", "Generator");

            // 配置热重载：配置变更时自动更新参数
            _optionsMonitor.OnChange(appSettings =>
            {
                _currentSimConfig = appSettings.Simulation;
                _sharedData.SetNoiseMultiplier(appSettings.Simulation.DefaultNoise);
                _sharedData.SetResponseDelay(appSettings.Simulation.DefaultDelayMs);
                if (Enum.TryParse<SharedData.SimulationMode>(appSettings.Simulation.InitialMode, out var mode))
                {
                    _sharedData.SetMode(mode);
                }
                _log.Information("[HotReload] Simulation config reloaded: Mode={Mode}, Noise={Noise}, Delay={Delay}",
                    appSettings.Simulation.InitialMode, appSettings.Simulation.DefaultNoise, appSettings.Simulation.DefaultDelayMs);
            });
        }

        /// <summary>
        /// 主循环：根据当前模式生成模拟数据并更新到 SharedData。
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 获取当前配置参数
                var mode = _sharedData.GetMode();
                var noise = _sharedData.GetNoiseMultiplier();
                var delay = _sharedData.GetResponseDelayMs();
                int timeout = _currentSimConfig.TimeoutMs;
                int interval = _currentSimConfig.UpdateIntervalMs;

                // 冻结模式：保持数值不变
                if (mode == SharedData.SimulationMode.Frozen)
                {
                    var snap = _sharedData.Snapshot();
                    _log.Debug("Frozen state: Temp={Temp}, Press={Press}, Status={Status}", snap.Temp, snap.Press, snap.Status);
                    await _timeProvider.Delay(timeout, stoppingToken);
                    continue;
                }

                // 生成基础随机数据（温度 20-30°C，压力 90-110 kPa）
                float t = (float)(_randomProvider.NextDouble() * 10.0 + 20.0);
                float p = (float)(_randomProvider.NextDouble() * 20.0 + 90.0);

                // 趋势模式：叠加正弦波模拟周期性变化
                if (mode == SharedData.SimulationMode.Trend)
                {
                    var seconds = _timeProvider.UtcNow.Second + _timeProvider.UtcNow.Minute * 60;
                    t += (float)Math.Sin(seconds / 60.0 * Math.PI * 2) * 2.0f;
                    p += (float)Math.Sin(seconds / 30.0 * Math.PI * 2) * 3.0f;
                }

                // 添加随机噪声
                t += (float)((_randomProvider.NextDouble() - 0.5) * 2.0 * noise);
                p += (float)((_randomProvider.NextDouble() - 0.5) * 2.0 * noise);
                bool s = _randomProvider.Next(2) == 1;

                // 模拟响应延迟
                if (delay > 0)
                {
                    try { await _timeProvider.Delay(delay, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }

                // 更新共享数据
                _sharedData.Update((float)Math.Round(t, 2), (float)Math.Round(p, 2), s);
                _log.Debug("Generated data: Temp={Temp}, Press={Press}, Status={Status}, Mode={Mode}, Noise={Noise}, DelayMs={DelayMs}",
                    t, p, s, mode, noise, delay);
                await _timeProvider.Delay(interval, stoppingToken);
            }
            _log.Information("Generator task stopped.");
        }
    }
}
