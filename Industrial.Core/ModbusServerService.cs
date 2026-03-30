using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Modbus.Data;
using Modbus.Device;
using Serilog;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Industrial.Core
{
    /// <summary>
    /// Modbus TCP 服务器服务：响应上位机读写请求，支持配置热重载。
    /// </summary>
    public class ModbusServerService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
        private ModbusSettings _currentModbusConfig;
        private readonly ILogger _log;
        private CancellationTokenSource? _restartCts;

        public ModbusServerService(
            SharedData sharedData,
            IOptionsMonitor<AppSettings> optionsMonitor)
        {
            _sharedData = sharedData;
            _optionsMonitor = optionsMonitor;
            _currentModbusConfig = optionsMonitor.CurrentValue.Modbus;
            _log = Log.ForContext("SourceContext", "ModbusServer");

            // 配置热重载：配置变更时自动更新参数
            _optionsMonitor.OnChange(appSettings =>
            {
                var newConfig = appSettings.Modbus;
                // 检测关键配置是否变化（需要重启服务的配置）
                if (newConfig.Port != _currentModbusConfig.Port ||
                    newConfig.SlaveId != _currentModbusConfig.SlaveId ||
                    newConfig.IpAddress != _currentModbusConfig.IpAddress)
                {
                    _log.Information("[HotReload] Critical config changed (Port/SlaveId/Ip), triggering server restart...");
                    _restartCts?.Cancel();
                }
                _currentModbusConfig = newConfig;
                _log.Information("[HotReload] Modbus config updated: Port={Port}, SlaveId={SlaveId}, Ip={Ip}",
                    newConfig.Port, newConfig.SlaveId, newConfig.IpAddress);
            });
        }

        /// <summary>
        /// 服务主循环：配置变更时自动重启服务器。
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 外层循环：支持配置变更后自动重启
            while (!stoppingToken.IsCancellationRequested)
            {
                _restartCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken, _restartCts.Token);

                await RunServerAsync(linkedCts.Token);

                // 如果是外部取消，直接退出
                if (stoppingToken.IsCancellationRequested)
                {
                    _log.Information("Server shutdown requested.");
                    break;
                }

                // 配置变更触发的重启
                _log.Information("Server restarting with new configuration...");
                try
                {
                    await Task.Delay(1000, stoppingToken); // 短暂延迟避免频繁重启
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 启动 Modbus TCP 服务器并处理客户端请求。
        /// </summary>
        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            var config = _currentModbusConfig;

            // 解析 IP 地址，带错误处理
            IPAddress ipAddress;
            try
            {
                ipAddress = System.Net.IPAddress.Parse(config.IpAddress);
            }
            catch (FormatException ex)
            {
                _log.Error(ex, "Invalid IP address format: {IpAddress}", config.IpAddress);
                return;
            }

            var endpoint = new IPEndPoint(ipAddress, config.Port);
            var listener = new TcpListener(endpoint);

            try
            {
                listener.Start();
                _log.Information("[Modbus] Listening on {Endpoint}", endpoint);
            }
            catch (SocketException ex)
            {
                _log.Error(ex, "Failed to start listener on {Endpoint}. Port may be in use.", endpoint);
                return;
            }

            try
            {
                var dataStore = DataStoreFactory.CreateDefaultDataStore();
                dataStore.DataStoreReadFrom += (obj, e) =>
                {
                    if (obj is not DataStore store) return;
                    if (e.ModbusDataType == ModbusDataType.HoldingRegister)
                    {
                        var registers = e.Data.B;
                        int count = registers.Count;
                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i);
                            if (addr >= store.HoldingRegisters.Count) continue;
                            ushort valueToWrite = 0;
                            bool shouldUpdate = true;
                            if (addr == RegisterMap.Temperature)
                                valueToWrite = _sharedData.GetTempReg();
                            else if (addr == RegisterMap.Pressure)
                                valueToWrite = _sharedData.GetPressReg();
                            else if (addr == RegisterMap.SimulationMode)
                                valueToWrite = (ushort)_sharedData.GetMode();
                            else if (addr == RegisterMap.NoiseMultiplier)
                            {
                                float val = _sharedData.GetNoiseMultiplier() * 100f;
                                valueToWrite = (ushort)(val < 0 ? 0 : (val > 65535 ? 65535 : val));
                            }
                            else if (addr == RegisterMap.ResponseDelayMs)
                            {
                                int val = _sharedData.GetResponseDelayMs();
                                valueToWrite = (ushort)(val < 0 ? 0 : (val > 65535 ? 65535 : val));
                            }
                            else
                            {
                                shouldUpdate = false;
                            }
                            if (shouldUpdate)
                                store.HoldingRegisters[addr + 1] = valueToWrite;
                        }
                    }
                    else if (e.ModbusDataType == ModbusDataType.Coil)
                    {
                        var coils = e.Data.A;
                        int count = coils.Count;
                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i);
                            if (addr >= store.CoilDiscretes.Count) continue;
                            if (addr == RegisterMap.StatusCoil)
                            {
                                bool status = _sharedData.GetStatusCoil();
                                store.CoilDiscretes[addr + 1] = status;
                            }
                        }
                    }
                };
                dataStore.DataStoreWrittenTo += (obj, e) =>
                {
                    _log.Information("Write request: Type={Type}, StartAddr={Addr}, Count={Count}",
                        e.ModbusDataType, e.StartAddress, e.Data.B.Count);
                    if (e.ModbusDataType == ModbusDataType.HoldingRegister)
                    {
                        var registers = e.Data.B;
                        int count = registers.Count;
                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i + 1);
                            ushort val = registers[i];
                            if (addr == RegisterMap.SimulationMode)
                            {
                                if (Enum.IsDefined(typeof(SharedData.SimulationMode), val))
                                    _sharedData.SetMode((SharedData.SimulationMode)val);
                            }
                            else if (addr == RegisterMap.NoiseMultiplier)
                            {
                                _sharedData.SetNoiseMultiplier(val / 100f);
                            }
                            else if (addr == RegisterMap.ResponseDelayMs)
                            {
                                _sharedData.SetResponseDelay(val);
                            }
                            else if (addr == RegisterMap.FaultInjectionControl)
                            {
                                // 故障注入控制
                                switch (val)
                                {
                                    case 0:
                                        _sharedData.ResumeNormal();
                                        _log.Information("[FaultInjection] Resumed normal operation");
                                        break;
                                    case 1:
                                        _sharedData.InjectFaultyTemperature();
                                        _log.Warning("[FaultInjection] Injected faulty temperature (999.9°C)");
                                        break;
                                    case 2:
                                        _sharedData.InjectFaultyPressure();
                                        _log.Warning("[FaultInjection] Injected faulty pressure (-50.0 kPa)");
                                        break;
                                    case 3:
                                        _sharedData.FreezeData();
                                        _log.Warning("[FaultInjection] Frozen data updates");
                                        break;
                                    default:
                                        _log.Warning("[FaultInjection] Unknown control code: {Code}", val);
                                        break;
                                }
                            }
                        }
                    }
                    else if (e.ModbusDataType == ModbusDataType.Coil)
                    {
                        var coils = e.Data.A;
                        int count = coils.Count;
                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i + 1);
                            bool val = coils[i];
                            if (addr == RegisterMap.StatusCoil)
                            {
                                _sharedData.SetStatus(val);
                            }
                        }
                    }
                };
                var slave = ModbusTcpSlave.CreateTcp(config.SlaveId, listener);
                slave.DataStore = dataStore;
                _log.Information("[Modbus] Server started with SlaveId={SlaveId}", config.SlaveId);

                var listenTask = Task.Run(async () =>
                {
                    try { await slave.ListenAsync(); }
                    catch (OperationCanceledException) { }
                    catch (ObjectDisposedException) { }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted) { }
                    catch (Exception ex) { _log.Error(ex, "Listen loop error"); }
                }, cancellationToken);

                await Task.WhenAny(listenTask, Task.Delay(-1, cancellationToken));

                if (cancellationToken.IsCancellationRequested)
                {
                    _log.Information("Server stopping (cancellation requested)...");
                }

                slave?.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Server error");
            }
            finally
            {
                listener.Stop();
                _log.Information("Server stopped on {Endpoint}", endpoint);
            }
        }
    }
}
