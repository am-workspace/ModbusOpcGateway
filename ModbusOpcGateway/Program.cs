using Industrial.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ModbusOpcGateway
{
    /// <summary>
    /// 应用程序入口：配置依赖注入并启动后台服务。
    /// </summary>
    public class Program
    {
        /// <summary>
        /// 程序入口点：构建 Host 并启动服务。
        /// </summary>
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService() // 启用 Windows 服务支持
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<AppSettings>(context.Configuration);
                    services.AddSingleton<SharedData>();
                    services.AddHostedService<GeneratorService>();
                    services.AddHostedService<ModbusServerService>();
                })
                .UseSerilog((context, services, configuration) =>
                {
                    configuration.ReadFrom.Configuration(context.Configuration);
                })
                .Build();

            // 只保留一次性初始化逻辑
            await RunMainLogic(host.Services);
            // 启动后台服务
            await host.RunAsync();
        }

        // 定义静态 Logger，全局可用
        private static readonly ILogger _log = Log.ForContext<Program>();

        /// <summary>
        /// 初始化逻辑：验证配置、应用初始设置、生成寄存器文档。
        /// </summary>
        private static async Task RunMainLogic(IServiceProvider services)
        {
            // 通过依赖注入获取配置和共享数据
            var appSettings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value;
            var shared = services.GetRequiredService<SharedData>();

            // 配置 Serilog 日志记录器
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(services.GetRequiredService<IConfiguration>())
                .CreateLogger();

            try
            {
                // 校验 SlaveId 合法性
                if (appSettings.Modbus.SlaveId < 1 || appSettings.Modbus.SlaveId > 247)
                {
                    _log.Error("Invalid Modbus Slave ID: {Id}. Must be between 1 and 247.", appSettings.Modbus.SlaveId);
                    return;
                }

                _log.Information("Loaded Config: Port={Port}, Id={Id}, Mode={Mode}",
                        appSettings.Modbus.Port,
                        appSettings.Modbus.SlaveId,
                        appSettings.Simulation.InitialMode);

                _log.Information("=== Modbus Slave Simulator Starting ===");

                // 应用初始配置
                if (Enum.TryParse<SharedData.SimulationMode>(appSettings.Simulation.InitialMode, out var mode))
                {
                    shared.SetMode(mode);
                }
                shared.SetNoiseMultiplier(appSettings.Simulation.DefaultNoise);
                shared.SetResponseDelay(appSettings.Simulation.DefaultDelayMs);

                _log.Information("=== Generating Register Map documentation ===");
                string markdown = RegisterMap.GenerateMarkdownTable();
                File.WriteAllText("REGISTER_MAP.md", markdown);
                _log.Information("=== Registered map saved to REGISTER_MAP.md ===");
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "Initialization failed");
            }
        }

    }
}
