using Microsoft.Extensions.Configuration;
using Modbus.Data;
using Modbus.Device;
using ModbusOpcGateway;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModernGateway
{
    public class Program
    {
        // 定义静态 Logger，全局可用
        private static readonly ILogger _log = Log.ForContext<Program>();
        // 定义专门的生成器日志上下文，方便区分来源
        //private static readonly ILogger _logGen = Log.ForContext("SourceContext", "Generator");

        /// <summary>
        /// 主程序运行入口
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            // 1. 加载配置
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // 设置基路径为当前目录
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // 加载 json，false 表示找不到就报错
                .Build();

            // 配置 Serilog 日志记录器
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .CreateLogger();

            try
            {
                // 2. 将配置绑定到强类型对象
                var appSettings = new AppSettings();
                config.Bind(appSettings);

                // 校验 SlaveId 合法性
                if (appSettings.Modbus.SlaveId < 1 || appSettings.Modbus.SlaveId > 247)
                {
                    _log.Error("Invalid Modbus Slave ID: {Id}. Must be between 1 and 247.", appSettings.Modbus.SlaveId);
                    // 可以选择强制修正为默认值，或者直接退出
                    // appSettings.Modbus.SlaveId = 1; 
                    return;
                }

                // 验证配置加载是否成功
                //Console.WriteLine($"[Config] Loaded settings. Modbus Port: {appSettings.Modbus.Port}, Mode: {appSettings.Simulation.InitialMode}");
                _log.Information("Loaded Config: Port={Port}, Id={Id}, Mode={Mode}",
                        appSettings.Modbus.Port,
                        appSettings.Modbus.SlaveId,
                        appSettings.Simulation.InitialMode);

                // 3. 程序启动欢迎信息
                //Console.WriteLine("Modbus Slave Simulator starting...");
                _log.Information("=== Modbus Slave Simulator Starting ===");

                // 4. 创建共享数据中心
                // 这是整个系统的“心脏”。
                // RunGenerator (写) 和 RunModbusServer (读/写) 都将通过引用访问同一个对象，实现数据互通。
                var shared = new SharedData();

                // 应用初始配置 (例如初始噪声、初始延迟)
                // 注意：模式可能需要通过枚举转换
                if (Enum.TryParse<SharedData.SimulationMode>(appSettings.Simulation.InitialMode, out var mode))
                {
                    shared.SetMode(mode);
                }
                shared.SetNoiseMultiplier(appSettings.Simulation.DefaultNoise);
                shared.SetResponseDelay(appSettings.Simulation.DefaultDelayMs);

                //输出md文件==再说
                //Console.WriteLine("Generating Register Map documentation...");
                _log.Information("=== Generating Register Map documentation ===");
                string markdown = RegisterMap.GenerateMarkdownTable();
                File.WriteAllText("REGISTER_MAP.md", markdown);
                //Console.WriteLine("Registered map saved to REGISTER_MAP.md");
                _log.Information("=== Registered map saved to REGISTER_MAP.md ===");


                // 5. 创建取消令牌源 (CancellationTokenSource)
                // 用于协调所有任务的优雅退出。当需要停止程序时，触发这个源，所有监听 token 的任务都会收到信号。
                using var cts = new CancellationTokenSource();

                // 6. 绑定控制台退出事件 (Ctrl + C)
                // 当用户在命令行按下 Ctrl+C 时：
                // - e.Cancel = true: 阻止系统强制杀死进程，让我们有机会清理资源。
                // - cts.Cancel(): 触发取消信号，通知后台任务停止运行。
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                // 7. 定义并发任务列表
                // 这里并没有立即执行任务，只是定义了要运行的两个异步操作：
                // - RunGenerator: 负责在后台不断制造假数据（模拟传感器）。
                // - RunModbusServer: 负责在后台监听网络端口，响应外部请求。
                // 它们将同时运行（并发），互不阻塞。
                var tasks = new[]
                {
                    RunGenerator(shared, cts.Token, appSettings.Simulation, new ModbusOpcGateway.TimeProvider(), new ModbusOpcGateway.RandomProvider()), // 传入仿真配置
                    RunModbusServer(shared, cts.Token, appSettings.Modbus)   // 传入 Modbus 配置
                };

                // 8. 提示服务已就绪
                _log.Information("Modbus TCP listening on {Ip}:{Port}",
                        appSettings.Modbus.IpAddress,
                        appSettings.Modbus.Port);
                //Console.WriteLine($"> Modbus TCP listening on {appSettings.Modbus.IpAddress}:{appSettings.Modbus.Port}");
                _log.Information("=== Press Ctrl+C to stop. ===");
                //Console.WriteLine("Press Ctrl+C to stop.");


                // 9. 等待所有任务完成
                // Task.WhenAll 会挂起当前主线程，直到数组中的【所有】任务都结束。
                // 由于这两个任务内部都是 while(!token) 的死循环，它们理论上永远不会自然结束，
                // 只有当 cts.Cancel() 被触发（用户按 Ctrl+C）时，它们才会抛出异常或退出循环，从而结束任务。
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // 10. 捕获取消异常
                // 当任务因接收到取消信号而退出时，通常会抛出此异常。
                // 我们捕获它以避免程序崩溃报错，而是优雅地打印停止信息。
                //Console.WriteLine("Stopped gracefully.");
                _log.Information("Shutdown requested by user.");
            }
            catch(Exception ex) 
            {
                // 【修改点】记录致命错误
                _log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                // 11. 程序退出前的清理 (可选)
                // using var cts 会自动在这里释放资源。
                //Console.WriteLine("Simulator shutdown complete.");
                _log.Information("Simulator shutdown complete.");
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// 模拟数据生成器：在后台持续产生假的传感器数据（温度、压力、状态），
        /// 并实时更新到 SharedData 中，供 Modbus 服务器读取。
        /// </summary>
        static async Task RunGenerator(SharedData data, CancellationToken token, SimulationSettings config, ITimeProvider timeProvider, IRandomProvider randomProvider)
        {
            // 创建一个专门的日志上下文，标明这是“生成器”模块的日志，方便区分来源
            var log = Log.ForContext("SourceContext", "Generator");

            // 初始化随机数生成器。
            // 使用固定种子 (42) 意味着每次重启程序，生成的随机序列是完全一样的。
            // 这有助于复现 Bug 和进行确定性测试。如果需要完全不同的随机性，可改为 new Random()。
            //var rand = new Random(42);
            //var rand = new Random(new Random().Next());

            // 使用配置中的超时刷新，而不是写死的 1000
            int timeout = config.TimeoutMs;
            // 使用配置中的更新间隔，而不是写死的 2000
            int interval = config.UpdateIntervalMs;

            // 主循环：只要取消令牌未被触发（即服务未停止），就持续生成数据
            while (!token.IsCancellationRequested)
            {
                // 1. 获取当前的控制参数
                // 这些参数可以由外部（如 Modbus 客户端写入）动态调整，实现运行时控制
                var mode = data.GetMode();              // 获取运行模式 (普通/趋势/冻结)
                var noise = data.GetNoiseMultiplier();  // 获取噪声系数 (影响数据波动幅度)
                var delay = data.GetResponseDelayMs();  // 获取模拟延迟 (模拟设备反应慢)

                // 2. 处理“冻结模式” (Frozen)
                // 模拟设备故障、维护暂停或用户手动锁定数值
                if (mode == SharedData.SimulationMode.Frozen)
                {
                    // 获取当前数据的快照（只读副本），避免直接操作共享对象时的线程安全问题
                    var snap = data.Snapshot();

                    // 打印日志，表明当前处于冻结状态，数值不再变化
                    //Console.WriteLine($"[Gen] Frozen Temp={snap.Temp:F2} Press={snap.Press:F2} Status={snap.Status}");
                    // 【修改点】使用结构化日志，而不是字符串拼接
                    // 注意：这里使用了 Debug 级别，因为这是高频日志
                    log.Debug("Frozen state: Temp={Temp}, Press={Press}, Status={Status}",
                        snap.Temp, snap.Press, snap.Status);

                    // 等待 1 秒后再次检查状态，避免死循环占用 CPU
                    await timeProvider.Delay(timeout, token);
                    continue; // 跳过本次循环剩余部分，不生成新数据
                }

                // 3. 生成基础随机数据
                // 模拟传感器的基础读数范围
                float t = (float)(randomProvider.NextDouble() * 10.0 + 20.0); // 温度：20.0 ~ 30.0 °C
                float p = (float)(randomProvider.NextDouble() * 20.0 + 90.0); // 压力：90.0 ~ 110.0 kPa

                // 4. 处理“趋势模式” (Trend)
                // 模拟物理过程的周期性变化（如加热炉升温降温、压缩机循环）
                if (mode == SharedData.SimulationMode.Trend)
                {
                    // 计算基于时间的正弦波偏移量
                    // seconds: 将分钟和秒转换为总秒数，作为时间轴
                    var seconds = timeProvider.UtcNow.Second + timeProvider.UtcNow.Minute * 60;

                    // 温度叠加正弦波：周期约 2 分钟，振幅 ±2.0
                    t += (float)Math.Sin(seconds / 60.0 * Math.PI * 2) * 2.0f;

                    // 压力叠加正弦波：周期约 1 分钟，振幅 ±3.0
                    p += (float)Math.Sin(seconds / 30.0 * Math.PI * 2) * 3.0f;
                }

                // 5. 添加随机噪声
                // 模拟真实传感器的信号抖动或环境干扰
                // noise 系数越大，数据跳动越剧烈
                t += (float)((randomProvider.NextDouble() - 0.5) * 2.0 * noise);
                p += (float)((randomProvider.NextDouble() - 0.5) * 2.0 * noise);

                // 生成随机布尔状态 (例如：开关状态、报警位)
                bool s = randomProvider.Next(2) == 1;

                // 6. 模拟响应延迟
                // 如果配置了延迟，先等待一段时间再更新数据
                // 这可以测试客户端在数据更新不及时时的超时处理逻辑
                if (delay > 0)
                {
                    try
                    {
                        await timeProvider.Delay(delay, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // 如果在等待期间收到取消信号，立即退出循环
                        break;
                    }
                }

                // 7. 更新共享数据
                // 将计算好的新值写入 SharedData。
                // 注意：这里进行了四舍五入，模拟真实设备的精度限制
                data.Update((float)Math.Round(t, 2), (float)Math.Round(p, 2), s);

                // 打印调试日志，显示当前生成的详细数据和使用的参数
                //Console.WriteLine($"[Gen] Temp={t:F2} Press={p:F2} Status={s} Mode={mode} Noise={noise} DelayMs={delay}");
                // 【修改点】记录生成的数据
                // 我们可以将 Temp 和 Press 作为属性记录，这样在文件中可以搜索 "Temp > 28"
                log.Debug("Generated data: Temp={Temp}, Press={Press}, Status={Status}, Mode={Mode}, Noise={Noise}, DelayMs={DelayMs}",
                    t, p, s, mode, noise, delay);

                // 8. 控制生成频率
                // 每隔 2 秒生成一次新数据，模拟真实的采样率
                await timeProvider.Delay(interval, token);
            }

            // 循环结束，意味着收到了取消信号，任务正常退出
            //Console.WriteLine("[Gen] Generator stopped.");
            log.Information("Generator task stopped.");
        }

        /// <summary>
        /// 通过data中的数据，动态更新 Modbus 数据存储 (DataStore)，并响应主站的读写请求。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        static async Task RunModbusServer(SharedData data, CancellationToken token, ModbusSettings config)
        {
            // 为服务器任务创建上下文 Logger
            var log = Log.ForContext("SourceContext", "ModbusServer");

            // 【关键修改】使用配置文件中的端口和 IP
            var ipAddress = System.Net.IPAddress.Parse(config.IpAddress);
            var endpoint = new IPEndPoint(ipAddress, config.Port);

            //var endpoint = new IPEndPoint(IPAddress.Any, 5020);
            var listener = new TcpListener(endpoint);
            listener.Start();
            Console.WriteLine($"[Modbus] Listening on {endpoint}");

            try
            {
                // 1. 创建 DataStore
                var dataStore = DataStoreFactory.CreateDefaultDataStore();

                // --- 读取事件 (Read From) ---
                // 逻辑：当主站请求读取时，我们先将最新的业务数据填入 dataStore 的内部数组，
                // 然后库会自动从数组中读取并发送给主站。
                dataStore.DataStoreReadFrom += (obj, e) =>
                {
                    // 将 obj 强制转换为具体的 DataStore 类型以访问内部数组
                    // 在 FluentModbus 中，obj 通常就是 DataStore 本身
                    if (obj is not DataStore store) return;

                    if (e.ModbusDataType == ModbusDataType.HoldingRegister)
                    {
                        // 【关键步骤 1】：从联合类型中提取 ushort 集合 (Case B)
                        var registers = e.Data.B;

                        // 【关键步骤 2】：获取数量
                        int count = registers.Count;

                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i);

                            // 边界检查：防止地址超出 DataStore 范围 (通常是 65535，但最好检查一下)
                            if (addr >= store.HoldingRegisters.Count) continue;

                            ushort valueToWrite = 0;
                            bool shouldUpdate = true;

                            if (addr == RegisterMap.Temperature)
                                valueToWrite = data.GetTempReg();
                            else if (addr == RegisterMap.Pressure)
                                valueToWrite = data.GetPressReg();
                            else if (addr == RegisterMap.SimulationMode)
                                valueToWrite = (ushort)data.GetMode();
                            else if (addr == RegisterMap.NoiseMultiplier)
                            {
                                float val = data.GetNoiseMultiplier() * 100f;
                                valueToWrite = (ushort)(val < 0 ? 0 : (val > 65535 ? 65535 : val));
                            }
                            else if (addr == RegisterMap.ResponseDelayMs)
                            {
                                int val = data.GetResponseDelayMs();
                                valueToWrite = (ushort)(val < 0 ? 0 : (val > 65535 ? 65535 : val));
                            }
                            else
                            {
                                // 未定义的地址，不更新，保持原值或默认0
                                shouldUpdate = false;
                            }

                            if (shouldUpdate)
                            {
                                // 【关键】：直接写入 DataStore 的内部数组
                                store.HoldingRegisters[addr] = valueToWrite;
                            }
                        }
                    }
                    else if (e.ModbusDataType == ModbusDataType.Coil)
                    {
                        // 【关键步骤 1】：从联合类型中提取 bool 集合 (Case A)
                        var coils = e.Data.A;

                        // 【关键步骤 2】：获取数量
                        int count = coils.Count;

                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i);
                            if (addr >= store.CoilDiscretes.Count) continue;

                            if (addr == RegisterMap.StatusCoil)
                            {
                                bool status = data.GetStatusCoil();
                                // 【关键】：直接写入 DataStore 的内部数组
                                store.CoilDiscretes[addr] = status;
                            }
                        }
                    }
                };

                // --- 写入事件 (Written To) ---
                // 逻辑：当主站写入时，e.Data 包含了主站发来的新值。
                // 我们遍历这些值，更新我们的业务逻辑对象 (data)，同时 DataStore 内部数组通常会自动更新
                // (取决于库的实现，但为了保险，我们主要关注更新业务对象)。
                dataStore.DataStoreWrittenTo += (obj, e) =>
                {
                    // 【修改点】记录写入操作，这对于审计非常重要
                    // 记录：谁（IP），写了什么地址，写了什么值
                    // 注意：这里拿不到客户端IP，除非通过更底层的流获取，这里仅记录值
                    log.Information("Write request: Type={Type}, StartAddr={Addr}, Count={Count}",
                        e.ModbusDataType, e.StartAddress, e.Data.B.Count);

                    if (e.ModbusDataType == ModbusDataType.HoldingRegister)
                    {
                        // 此时 e.Data 包含的是主站写入的新值 (ReadOnlyCollection<ushort>)
                        // 注意：在 FluentModbus 中，DataStore 内部数组通常已经在事件触发前被更新了。
                        // 我们这里主要目的是同步到 SharedData 对象。

                        // 提取 ushort 集合 (Case B)
                        var registers = e.Data.B;
                        int count = registers.Count;
                        // 如果需要获取具体值，可以通过索引访问 e.Data[i]
                        // 但 ReadOnlyCollection 支持索引器

                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i);
                            ushort val = registers[i]; // 读取主站写入的值

                            if (addr == RegisterMap.SimulationMode)
                            {
                                // 简单的枚举转换保护
                                if (Enum.IsDefined(typeof(SharedData.SimulationMode), val))
                                    data.SetMode((SharedData.SimulationMode)val);
                            }
                            else if (addr == RegisterMap.NoiseMultiplier)
                            {
                                data.SetNoiseMultiplier(val / 100f);
                            }
                            else if (addr == RegisterMap.ResponseDelayMs)
                            {
                                data.SetResponseDelay(val);
                            }
                        }
                    }
                    else if (e.ModbusDataType == ModbusDataType.Coil)
                    {
                        // 提取 bool 集合 (Case A)
                        var coils = e.Data.A;
                        int count = coils.Count;
                        for (int i = 0; i < count; i++)
                        {
                            ushort addr = (ushort)(e.StartAddress + i);
                            bool val = coils[i]; // 直接是 bool

                            if (addr == RegisterMap.StatusCoil)
                            {
                                data.SetStatus(val);
                            }
                        }
                    }
                };

                // 2. 创建从站并挂接 DataStore
                // 注意：FluentModbus 的 CreateTcp 通常需要传入 listener 或者 port
                // 如果 CreateTcp(1, listener) 报错，请尝试 CreateTcp(1, endpoint.Port) 并手动 Accept
                // 但根据你的代码片段，假设 CreateTcp(1, listener) 是你库的正确用法。
                // 在某些版本中，可能是: var slave = ModbusTcpSlave.Create(1, listener);
                var slave = ModbusTcpSlave.CreateTcp(config.SlaveId, listener);
                slave.DataStore = dataStore;

                Console.WriteLine("[Modbus] Server started with dynamic data binding.");

                var listenTask = Task.Run(async () =>
                {
                    try
                    {
                        // 启动监听
                        await slave.ListenAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消
                    }
                    catch (ObjectDisposedException)
                    {
                        // 正常关闭
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                    {
                        // 正常关闭
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine($"[Modbus] Listen error: {ex.Message}");
                        // 【修改点】记录异常
                        log.Error(ex, "Listen loop error");
                    }
                }, token);

                // 等待取消或任务结束
                await Task.WhenAny(listenTask, Task.Delay(-1, token));

                //Console.WriteLine("[Modbus] Cancellation requested, stopping server...");
                log.Information("Cancellation requested, stopping server...");

                // 清理资源
                slave?.Dispose();
            }
            catch (Exception ex)
            {
                log.Error(ex, "Server startup error");
            }
            finally
            {
                listener.Stop();
                //Console.WriteLine("[Modbus] Server stopped.");
                log.Information("Server stopped.");
            }
        }
    }
}
