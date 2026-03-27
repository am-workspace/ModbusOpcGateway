using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ModernGateway
{
    // 定义配置映射类
    public class AppSettings
    {
        public ModbusSettings Modbus { get; set; } = new();
        public SimulationSettings Simulation { get; set; } = new();
        public SerilogSettings Serilog { get; set; } = new();
    }

    public class ModbusSettings
    {
        [Range(1024, 65535)]
        public int Port { get; set; } = 5020;
        [Range(1, 247)]
        public byte SlaveId { get; set; } = 1;
        public string IpAddress { get; set; } = "0.0.0.0";
    }

    public class SimulationSettings
    {
        public string InitialMode { get; set; } = "Random";
        public int TimeoutMs { get; set; } = 1000;
        public int UpdateIntervalMs { get; set; } = 2000;
        public float DefaultNoise { get; set; } = 1.0f;
        public int DefaultDelayMs { get; set; } = 0;
    }

    public class SerilogSettings
    {
        public string MinimumLevel { get; set; } = "Information";
    }

    public class AppSettingsMap
    {
        
    }
}
