using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Industrial.Core
{
    /// <summary>
    /// 寄存器映射元数据：用于描述每个寄存器的详细信息，方便生成文档。
    /// </summary>
    public class RegisterDefinition
    {
        public ushort Address { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // "HoldingRegister", "Coil"
        public bool IsWritable { get; set; } = false;
        public string Description { get; set; } = "";
        public string Unit { get; set; } = "";
        public string ScaleInfo { get; set; } = ""; // 例如："Value / 10"
    }

    /// <summary>
    /// 寄存器地址常量定义中心。
    /// 所有 Modbus 地址必须在此定义，禁止在业务逻辑中直接使用数字字面量。
    /// </summary>
    public static class RegisterMap
    {
        // --- 保持寄存器 (Holding Registers, 功能码 03/06/16) ---

        /// <summary>
        /// 地址 0: 温度值 (放大10倍)
        /// </summary>
        public const ushort Temperature = 0;

        /// <summary>
        /// 地址 1: 压力值 (放大10倍)
        /// </summary>
        public const ushort Pressure = 1;

        /// <summary>
        /// 地址 10: 模拟模式 (0=Random, 1=Trend, 2=Frozen)
        /// </summary>
        public const ushort SimulationMode = 10;

        /// <summary>
        /// 地址 11: 噪声系数 (放大100倍)
        /// </summary>
        public const ushort NoiseMultiplier = 11;

        /// <summary>
        /// 地址 12: 响应延迟 (ms)
        /// </summary>
        public const ushort ResponseDelayMs = 12;

        // --- 故障模拟寄存器 (Holding Registers, 功能码 06/16) ---

        /// <summary>
        /// 地址 100: 故障注入控制 (0=恢复正常, 1=异常温度, 2=异常压力, 3=冻结数据)
        /// 用于测试上位机的异常处理能力。
        /// </summary>
        public const ushort FaultInjectionControl = 100;

        // --- 线圈 (Coils, 功能码 01/05/15) ---

        /// <summary>
        /// 地址 0: 设备状态 (True=运行, False=停止)
        /// </summary>
        public const ushort StatusCoil = 0;

        // --- 文档生成辅助方法 ---

        /// <summary>
        /// 获取所有寄存器的定义列表，用于生成文档或验证。
        /// </summary>
        public static List<RegisterDefinition> GetAllDefinitions()
        {
            return new List<RegisterDefinition>
            {
                new RegisterDefinition {
                    Address = Temperature,
                    Name = "Temperature",
                    Type = "HoldingRegister",
                    IsWritable = false,
                    Description = "当前环境温度",
                    Unit = "°C",
                    ScaleInfo = "寄存器值 / 10.0"
                },
                new RegisterDefinition {
                    Address = Pressure,
                    Name = "Pressure",
                    Type = "HoldingRegister",
                    IsWritable = false,
                    Description = "当前系统压力",
                    Unit = "kPa",
                    ScaleInfo = "寄存器值 / 10.0"
                },
                new RegisterDefinition {
                    Address = SimulationMode,
                    Name = "SimulationMode",
                    Type = "HoldingRegister",
                    IsWritable = true,
                    Description = "模拟器运行模式 (0:Random, 1:Trend, 2:Frozen)",
                    Unit = "Enum",
                    ScaleInfo = "直接写入枚举整数值"
                },
                new RegisterDefinition {
                    Address = NoiseMultiplier,
                    Name = "NoiseMultiplier",
                    Type = "HoldingRegister",
                    IsWritable = true,
                    Description = "数据波动噪声系数",
                    Unit = "Factor",
                    ScaleInfo = "寄存器值 / 100.0"
                },
                new RegisterDefinition {
                    Address = ResponseDelayMs,
                    Name = "ResponseDelayMs",
                    Type = "HoldingRegister",
                    IsWritable = true,
                    Description = "模拟响应延迟时间",
                    Unit = "ms",
                    ScaleInfo = "直接写入毫秒数"
                },
                new RegisterDefinition {
                    Address = StatusCoil,
                    Name = "StatusCoil",
                    Type = "Coil",
                    IsWritable = true,
                    Description = "设备运行状态开关",
                    Unit = "Boolean",
                    ScaleInfo = "0=Off, 1=On"
                },
                new RegisterDefinition {
                    Address = FaultInjectionControl,
                    Name = "FaultInjectionControl",
                    Type = "HoldingRegister",
                    IsWritable = true,
                    Description = "故障注入控制 (0:正常, 1:异常温度, 2:异常压力, 3:冻结数据)",
                    Unit = "Enum",
                    ScaleInfo = "直接写入控制码"
                }
            };
        }

        /// <summary>
        /// 生成 Markdown 格式的寄存器映射表。
        /// 可以直接输出到控制台或写入文件。
        /// </summary>
        public static string GenerateMarkdownTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Modbus Register Map");
            sb.AppendLine();
            sb.AppendLine("自动生成的寄存器映射文档。请勿手动修改此文件，请更新 `RegisterMap.cs` 后重新生成。");
            sb.AppendLine();
            sb.AppendLine("| 地址 (Addr) | 名称 (Name) | 类型 (Type) | 读写 (R/W) | 单位 (Unit) | 缩放/说明 (Scale/Note) | 描述 (Description) |");
            sb.AppendLine("| :--- | :--- | :--- | :---: | :---: | :--- | :--- |");

            var regs = GetAllDefinitions().OrderBy(r => r.Type).ThenBy(r => r.Address);

            foreach (var reg in regs)
            {
                string rw = reg.IsWritable ? "RW" : "RO";
                sb.AppendLine($"| {reg.Address} | `{reg.Name}` | {reg.Type} | {rw} | {reg.Unit} | {reg.ScaleInfo} | {reg.Description} |");
            }

            sb.AppendLine();
            sb.AppendLine("## 枚举值参考");
            sb.AppendLine("### SimulationMode (地址 10)");
            sb.AppendLine("- `0`: Random (随机波动)");
            sb.AppendLine("- `1`: Trend (正弦波趋势)");
            sb.AppendLine("- `2`: Frozen (数值冻结)");
            sb.AppendLine();
            sb.AppendLine("### FaultInjectionControl (地址 100)");
            sb.AppendLine("- `0`: ResumeNormal (恢复正常)");
            sb.AppendLine("- `1`: FaultyTemperature (异常温度 999.9°C)");
            sb.AppendLine("- `2`: FaultyPressure (异常压力 -50.0 kPa)");
            sb.AppendLine("- `3`: FreezeData (冻结数据更新)");

            return sb.ToString();
        }
    }
}
